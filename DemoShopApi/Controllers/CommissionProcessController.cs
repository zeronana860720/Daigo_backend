using DemoShopApi.DTOs;
using DemoShopApi.Models;
using DemoShopApi.services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using DemoShopApi.Data;
using Microsoft.AspNetCore.Authorization;

namespace API.Controllers
{
    [Route("Commission")]
    public class CommissionProcessController : ControllerBase
    {
        private readonly DaigoContext _proxyContext;
        private readonly CommissionService _CommissionService;
        private readonly CreateCommissionCode _CreateCode;
        public CommissionProcessController(DaigoContext proxyContext, CommissionService commissionService, CreateCommissionCode CreateCode)
        {
            _proxyContext = proxyContext;
            _CommissionService = commissionService;
            _CreateCode = CreateCode;
        }

        //新增委託 -> 錢包確認 扣款
        // done
        [Authorize]
[HttpPost("Create")]
public async Task<IActionResult> CreateCommission([FromForm] CommissionCreateDto dto)
{
    // 1. 驗證資料格式
    if (!ModelState.IsValid)
    {
        return BadRequest(new { success = false, errors = ModelState });
    }

    // 2. 取得使用者資訊
    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId)) return Unauthorized(new { success = false, message = "請重新登入" });

    var user = await _proxyContext.Users.FirstOrDefaultAsync(u => u.Uid == userId);
    if (user == null) return Unauthorized(new { success = false, message = "找不到使用者" });

    // 3. 匯率計算
    var rates = new Dictionary<string, decimal> { { "JPY", 0.201m }, { "TWD", 1.0m }, { "USD", 32.5m } };
    decimal currentRate = rates.ContainsKey(dto.Currency ?? "TWD") ? rates[dto.Currency!] : 1.0m;
    
    // 計算商品原價台幣
    decimal subtotalTwd = (dto.Price * dto.Quantity) * currentRate;
    
    // 計算最低報酬 (商品總價的 20%)
    decimal minFeeTwd = Math.Round(subtotalTwd * 0.2m, 0, MidpointRounding.AwayFromZero);
    
    // 檢查使用者輸入的報酬是否符合最低標準
    if (dto.Fee < minFeeTwd)
    {
        return BadRequest(new { 
            success = false, 
            code = "FEE_TOO_LOW", 
            message = $"報酬至少需要 NT${minFeeTwd} (商品總價的 20%)" 
        });
    }
    
    // 總扣款金額 = 商品原價台幣 + 使用者輸入的報酬
    decimal totalPriceTwd = Math.Round(subtotalTwd + dto.Fee, 0, MidpointRounding.AwayFromZero);

    // 4. 餘額檢查
    if (user.Balance < totalPriceTwd)
    {
        return BadRequest(new { success = false, code = "BALANCE_NOT_ENOUGH", message = "錢包餘額不足" });
    }

    using var transaction = await _proxyContext.Database.BeginTransactionAsync();
    try
    {
        // 5. 扣錢
        user.Balance -= totalPriceTwd;

        // 6. 處理圖片路徑
        string? imageUrl = null;
        string? absolutePath = null;
        if (dto.Image != null && dto.Image.Length > 0)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
            absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
            imageUrl = $"/uploads/{fileName}";
        }

        // 7. 建立委託實體
        var commission = new Commission
        {
            CreatorId = userId,
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            Location = dto.Location,
            Price = dto.Price,
            Currency = dto.Currency ?? "TWD",
            Fee = dto.Fee,  // 使用者輸入的報酬
            EscrowAmount = totalPriceTwd,
            Quantity = dto.Quantity,
            Deadline = dto.Deadline.AddDays(7),
            Status = "審核中",
            CreatedAt = DateTime.Now,
            ImageUrl = imageUrl,
            Place = new CommissionPlace
            {
                Name = dto.Location,
                GooglePlaceId = dto.google_place_id ?? "",
                FormattedAddress = dto.formatted_address ?? "",
                Latitude = dto.latitude ?? 0m,
                Longitude = dto.longitude ?? 0m,
                CreatedAt = DateTime.Now
            }
        };

        // 8. 產生 ServiceCode
        await _CreateCode.CreateCommissionCodeAsync(commission);

        // 9. 建立扣款紀錄日誌
        var walletLog = new WalletLog
        {
            Uid = userId,
            Action = "CommissionPay",
            Amount = -totalPriceTwd,
            Balance = user.Balance ?? 0m,
            EscrowBalance = totalPriceTwd,
            ServiceCode = commission.ServiceCode,
            Description = commission.Title,
            CreatedAt = DateTime.Now
        };

        _proxyContext.WalletLogs.Add(walletLog);
        _proxyContext.Commissions.Add(commission);
        await _proxyContext.SaveChangesAsync();

        // 10. 記錄歷史
        var history = new CommissionHistory
        {
            CommissionId = commission.CommissionId,
            Action = "CREATE",
            ChangedBy = userId,
            NewData = "建立新委託"
        };
        _proxyContext.CommissionHistories.Add(history);
        await _proxyContext.SaveChangesAsync();

        // 11. 儲存實體檔案
        if (dto.Image != null && absolutePath != null)
        {
            using var stream = new FileStream(absolutePath, FileMode.Create);
            await dto.Image.CopyToAsync(stream);
        }

        await transaction.CommitAsync();
        return Ok(new { success = true, data = new { serviceCode = commission.ServiceCode } });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return StatusCode(500, new { success = false, message = "建立失敗", error = ex.Message });
    }
}


        //編輯委託 -> postpone
        [HttpPut("{ServiceCode}/Edit")]
        public async Task<IActionResult> EditCommission(string ServiceCode, [FromForm] CommissionEditDto dto)
        {
            var commissionId = await _proxyContext.Commissions
                                                .Where(c => c.ServiceCode == ServiceCode)
                                                .Select(c => c.CommissionId)
                                                .FirstOrDefaultAsync();

            if (commissionId == 0)
                return NotFound("找不到委託");

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    errors = ModelState
                                    .Where(x => x.Value.Errors.Count > 0)
                                    .ToDictionary(
                                    k => k.Key,
                                    v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                                )
                });
            }


            //id = 11; //模擬Commission id
            // 模擬user  之後要改session
            var userid = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userid))
            {
                return Unauthorized("尚未登入或憑證無效 (＞x＜)");
            }

            var (success, message) = await _CommissionService
                                                         .EditCommissionAsync(commissionId, userid, dto);
            if (!success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = message
                });
            }

            return Ok(new
            {
                success = true,
                message = message
            });
        }

        //接受委託-> done 
        [HttpPost("{ServiceCode}/accept")]
        public async Task<IActionResult> acceptCommission(string ServiceCode)
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var userid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userid))
            {
                return Unauthorized(new { success = false, message = "請先登入後再接單" });
            }
            using var transaction = await _proxyContext.Database.BeginTransactionAsync();
            try
            {
                var commission = await _proxyContext.Commissions
                                .Where(c => c.ServiceCode == ServiceCode)
                                .Select(c => new
                                {
                                    c.CommissionId,
                                    c.CreatorId,
                                    c.EscrowAmount,
                                    c.Status
                                }).FirstOrDefaultAsync();

                if (commission == null || commission.Status != "待接單")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "訂單不存在或無法接單"
                    });
                }


                var affected = await _proxyContext.Database.ExecuteSqlRawAsync(@"
                          UPDATE Commission
                          SET Status = '已接單',
                          UpdatedAt = GETDATE()
                          WHERE 
                          commission_id = @id
                          AND status = '待接單'
                          AND creator_id <> @userId
                            ",
                    new SqlParameter("@id", commission.CommissionId),
                    new SqlParameter("@userId", userid)
                    );

                if (affected == 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new
                    {
                        success = false,
                        message = "訂單已被接取或無法接單"
                    });
                }
                var newDiff = new Dictionary<string, object>();
                var oldDiff = new Dictionary<string, object>();
                var order = new CommissionOrder
                {
                    CommissionId = commission.CommissionId,
                    SellerId = userid,
                    BuyerId = commission.CreatorId,
                    Status = "PENDING", //未完成
                    Amount = commission.EscrowAmount??0,
                    
                    CreatedAt = DateTime.Now
                };
                _proxyContext.CommissionOrders.Add(order);

                var jsonOptions = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                newDiff["status"] = "已接單";
                oldDiff["status"] = "待接單";
                var history = new CommissionHistory
                {
                    CommissionId = commission.CommissionId,
                    Action = "ACCEPT",
                    ChangedBy = userid,
                    ChangedAt = DateTime.Now,
                    OldData = JsonSerializer.Serialize(oldDiff, jsonOptions),
                    NewData = JsonSerializer.Serialize(newDiff, jsonOptions)
                };


                _proxyContext.CommissionHistories.Add(history);


                await _proxyContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "訂單接受"
                });

            }
            //catch
            //{
            //    await transaction.RollbackAsync();
            //    return BadRequest(new
            //    {
            //        success = false,
            //        message = "接取訂單失敗，或是訂單已被接取"
            //    });
            //}
            catch (Exception ex) //如果報錯可以用
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }


        }


        //上傳明細
        [HttpPost("{ServiceCode}/receipt")]
        public async Task<IActionResult> UploadReceipt(string ServiceCode, [FromForm] UploadReceiptDto dto)
        {
            // 1. ✨ 修改：拿掉測試用的 "102"，嚴格檢查身分
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { success = false, message = "請先登入" });

            var commissionId = await _proxyContext.Commissions
                                                 .Where(c => c.ServiceCode == ServiceCode)
                                                 .Select(c => c.CommissionId)
                                                 .FirstOrDefaultAsync();
            if (commissionId == 0)
            {
                return NotFound(new { success = false, message = "委託不存在" });
            }

            using var tx = await _proxyContext.Database.BeginTransactionAsync();

            // 2. 驗證是否為接單者
            var order = await _proxyContext.CommissionOrders
                .FirstOrDefaultAsync(o => o.CommissionId == commissionId && o.SellerId == userId);

            if (order == null)
                return Forbid(); // 403 Forbidden: 你不是接單者

            var commission = await _proxyContext.Commissions
                .FirstOrDefaultAsync(c => c.CommissionId == commissionId);

            if (commission == null) return NotFound("委託不存在");
            
            // 3. 驗證圖片
            if (dto.Image == null || dto.Image.Length == 0) 
            { 
                return BadRequest(new { success = false, message = "請務必上傳收據照片" }); 
            }

            // 狀態檢查
            if (commission.Status != "已接單" && commission.Status != "出貨中")
                return BadRequest(new { success = false, message = "目前狀態不可上傳明細" });

            var commissionReceipt = await _proxyContext.CommissionReceipts
                                   .FirstOrDefaultAsync(c => c.CommissionId == commissionId);
            
            bool isFirstUpload = commissionReceipt == null;
            var oldremark = commissionReceipt?.Remark;

            // 4. 存圖片 (維持原樣)
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
            // 建議把 receipts 資料夾放在 uploads 下面管理，或者維持你原本的 wwwroot/receipts 也可以
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "receipts", fileName);
            
            // 確保資料夾存在
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var stream = new FileStream(path, FileMode.Create);
            await dto.Image.CopyToAsync(stream);

            var newImageUrl = $"/receipts/{fileName}";

            if (isFirstUpload)
            {
                commissionReceipt = new CommissionReceipt
                {
                    CommissionId = commissionId,
                    UploadedBy = userId
                };
                _proxyContext.CommissionReceipts.Add(commissionReceipt);
            }

            // 5. ✨ 重點修改：日期處理
            // 如果前端傳來的 ReceiptDate 是預設值(0001/1/1) 或 null，就使用當下時間
            // 假設你的 DTO 裡 ReceiptDate 是 DateTime? (Nullable)
            commissionReceipt.ReceiptDate = dto.ReceiptDate ?? DateTime.Now;
            
            // 如果你的 DTO 裡 ReceiptDate 是 DateTime (非 Nullable)，請改成這樣判斷：
            // commissionReceipt.ReceiptDate = dto.ReceiptDate == default ? DateTime.Now : dto.ReceiptDate;

            commissionReceipt.ReceiptImageUrl = newImageUrl;
            commissionReceipt.ReceiptAmount = dto.ReceiptAmount;
            commissionReceipt.Remark = dto.Remark;
            commissionReceipt.UploadedAt = DateTime.Now; // 確保更新上傳時間

            // 6. 更新狀態
            var oldStatus = commission.Status;
            if (commission.Status == "已接單")
            {
                commission.Status = "出貨中";
                commission.UpdatedAt = DateTime.Now;
            }

            // 7. 寫入歷史紀錄 (這部分寫得很棒！維持原樣)
            var oldDiff = new Dictionary<string, object>();
            var newDiff = new Dictionary<string, object>();

            oldDiff["imageurl"] = (isFirstUpload ? "null" : commissionReceipt.ReceiptImageUrl);
            newDiff["imageurl"] = newImageUrl;

            if (oldStatus != commission.Status)
            {
                oldDiff["status"] = oldStatus;
                newDiff["status"] = "出貨中";
            }
            if (oldremark != dto.Remark)
            {
                oldDiff["remark"] = oldremark;
                newDiff["remark"] = dto.Remark;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            if (oldDiff.Any())
            {
                _proxyContext.CommissionHistories.Add(new CommissionHistory
                {
                    CommissionId = commissionId,
                    Action = (oldStatus == "已接單" ? "UPLOAD_RECEIPT" : "REUPLOAD_RECEIPT"),
                    ChangedBy = userId,
                    ChangedAt = DateTime.Now,
                    OldData = JsonSerializer.Serialize(oldDiff, jsonOptions),
                    NewData = JsonSerializer.Serialize(newDiff, jsonOptions)
                });
            }

            await _proxyContext.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { success = true, message = (oldStatus == "已接單" ? "明細上傳成功" : "明細重新上傳成功") });
        }


        //寄貨後按鈕-> done 
        [HttpPost("{ServiceCode}/ship")]
        public async Task<IActionResult> ShipCommission(string ServiceCode, [FromBody] CommissionShipDto dto)
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? "102"; // Swagger 測試用
            var commissionId = await _proxyContext.Commissions
                                                .Where(c => c.ServiceCode == ServiceCode)
                                                .Select(c => c.CommissionId)
                                                .FirstOrDefaultAsync();
            if (commissionId == 0)
            {
                return NotFound("委託不存在");
            }
            using var tx = await _proxyContext.Database.BeginTransactionAsync();

            // 1️ 驗證接單者
            var order = await _proxyContext.CommissionOrders
                .FirstOrDefaultAsync(o => o.CommissionId == commissionId && o.SellerId == userId);

            if (order == null)
                return Forbid("你不是接單者");

            // 2️ 驗證委託
            var commission = await _proxyContext.Commissions
                .FirstOrDefaultAsync(c => c.CommissionId == commissionId);

            if (commission == null)
                return NotFound("委託不存在");

            if (commission.Status != "出貨中" && commission.Status != "已寄出")
                return BadRequest("目前狀態不可更改");

            // 3️ 取得寄貨資料（只會有一筆）
            var shipping = await _proxyContext.CommissionShippings
                .FirstOrDefaultAsync(s => s.CommissionId == commissionId);

            var oldTrackingNumber = shipping?.TrackingNumber; //舊的nunber
            var oldLogistics = shipping?.LogisticsName;//舊的Name       
            var oldstatus = commission.Status; //出貨中
            var oldRemark = shipping?.Remark;

            bool isFirstShip = shipping == null;

            if (isFirstShip)
            {
                shipping = new CommissionShipping
                {
                    CommissionId = commissionId,
                    ShippedBy = userId,
                    Status = "已寄出"
                };
                _proxyContext.CommissionShippings.Add(shipping);
            }
            if (isFirstShip)
            {
                commission.Status = "已寄出";
                commission.UpdatedAt = DateTime.Now;
            }

            // 4️ 更新寄貨資訊
            shipping.Status = "已寄出";
            shipping.ShippedAt = DateTime.Now;
            shipping.LogisticsName = dto.LogisticsName;
            shipping.TrackingNumber = dto.TrackingNumber;
            shipping.Remark = dto.Remark;



            // 5History diff
            var oldDiff = new Dictionary<string, object>();
            var newDiff = new Dictionary<string, object>();

            oldDiff["shipping_status"] = isFirstShip ? "出貨中" : "已寄出";
            newDiff["shipping_status"] = "已寄出";
            if (oldLogistics != dto.LogisticsName)
            {
                oldDiff["logistics"] = oldLogistics;
                newDiff["logistics"] = dto.LogisticsName;
            }
            if (oldTrackingNumber != dto.TrackingNumber)
            {
                oldDiff["tracking_number"] = oldTrackingNumber;
                newDiff["tracking_number"] = dto.TrackingNumber;
            }
            if (oldstatus != shipping.Status)
            {
                oldDiff["commissionstatus"] = oldstatus; //出貨中
                newDiff["commissionstatus"] = shipping.Status;
            }
            if (oldRemark != dto.Remark)
            {
                oldDiff["remark"] = oldRemark;
                newDiff["remark"] = dto.Remark;
            }
            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            if (oldDiff.Any())
            {
                _proxyContext.CommissionHistories.Add(new CommissionHistory
                {
                    CommissionId = commissionId,
                    Action = isFirstShip ? "SHIP_COMMISSION" : "RESHIP_COMMISSION",
                    ChangedBy = userId,
                    ChangedAt = DateTime.Now,
                    OldData = JsonSerializer.Serialize(oldDiff, jsonOptions),
                    NewData = JsonSerializer.Serialize(newDiff, jsonOptions)
                });
            }
            await _proxyContext.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                success = true,
                message = isFirstShip ? "寄貨成功" : "寄貨資訊更新成功"
            });
        }

        //完成訂單 (買家) -> ongoing
        [HttpPost("{ServiceCode}/complete")]
        public async Task<IActionResult> CompleteCommission(string ServiceCode)
        {
            // 從token找id
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "101";
            
            /*
             * 開啟交易
             * 確保兩筆更動都成功
             * 1.更新資料庫狀態
             * 2.撥款
             */
            using var tx = await _proxyContext.Database.BeginTransactionAsync();
            
            // 確認委託是否存在
            var commission = await _proxyContext.Commissions
                .FirstOrDefaultAsync(c => c.ServiceCode == ServiceCode);

            if (commission == null)
                return NotFound("委託不存在");
            // 身份認證
            if (commission.CreatorId != userId)
                return Forbid("你不是此委託的建立者");
            // 判斷狀態
            if (commission.Status != "已寄出")
                return BadRequest("目前狀態不可完成");
            // 撈commissionOrder表格
            // 確保委託跟訂單是對應
            var order = await _proxyContext.CommissionOrders
                .FirstOrDefaultAsync(o => o.CommissionId == commission.CommissionId);
            // 委託不存在或是還在pending
            if (order == null || order.Status != "PENDING")
                return BadRequest("訂單紀錄不存在或訂單尚未完成寄貨");
            
            var oldStatus = commission.Status; 
            //已寄出 狀態紀錄
            
            // 臨時的小帳本
            var paymentInfo = new
            {
                // orderAmount = order.Amount, -> 暫時先放著更改邏輯
                payoutAmount = commission.Fee, // 把這邊改成給小幫手的錢
                // releaseToSeller = order.Amount - commission.Fee
                currency = commission.Currency,
            };

            // 狀態更新
            commission.Status = "已完成";
            commission.UpdatedAt = DateTime.Now;
            order.Status = "COMPLETED";
            order.FinishedAt = DateTime.Now;

            // 金流
            var paymentService = new CommissionPaymentService(_proxyContext);
            // 呼叫依賴式注入-> 去撥款
            await paymentService.ReleaseToSellerAsync(commission.CommissionId);
            
            // 獲取賣家資訊 (為了拿他目前的 Balance 餘額)
            // 邏輯：透過 order 找到 SellerId，再從資料庫找這個使用者
            var seller = await _proxyContext.Users.FirstOrDefaultAsync(u => u.Uid == order.SellerId);

            if (seller != null) // 安全起見檢查一下賣家是否存在
            {
                // 2. ✨ 建立賣家的錢包紀錄
                var walletLog = new WalletLog
                {
                    Uid = seller.Uid,
                    Action = "CommissionIncome",       // 動作：委託收入
                    Amount = commission.Fee??0m,           // 收入金額 (正值)
                    Balance = seller.Balance ?? 0m,     // 撥款後的新餘額
                    EscrowBalance = 0m,                // 錢已經從代管轉出，所以代管餘額為 0
                    ServiceCode = commission.ServiceCode,
                    Description = $"委託完成收入：{commission.Title}", // 清楚的描述
                    CreatedAt = DateTime.Now
                };

                _proxyContext.WalletLogs.Add(walletLog);
            }

            // History
            var oldDiff = new Dictionary<string, object>();
            var newDiff = new Dictionary<string, object>();
            if (oldStatus != commission.Status)
            {
                oldDiff["status"] = oldStatus;
                newDiff["status"] = commission.Status;
            }
            // 把剛剛的小帳本存進去
            newDiff["payment"] = paymentInfo;


            // 設定json格式,確保中文不會亂碼
            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            // 只要有任何變動,新增一筆紀錄
            if (oldDiff.Any())
            {
                _proxyContext.CommissionHistories.Add(new CommissionHistory
                {
                    CommissionId = commission.CommissionId,
                    Action = "COMPLETE_COMMISSION",
                    ChangedBy = userId,
                    OldData = JsonSerializer.Serialize(oldDiff, jsonOptions),
                    NewData = JsonSerializer.Serialize(newDiff, jsonOptions)
                });
            }
            await _proxyContext.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { success = true, message = "訂單已完成" });
        }


        //商品瑕疵 取消 委託人必須退貨給 接委託人(承擔成本)
        [HttpPost("{ServiceCode}/cancel")]
        public async Task<IActionResult> CancelCommission(string ServiceCode, [FromBody] CommissionCancelDto dto)
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "101";

            using var tx = await _proxyContext.Database.BeginTransactionAsync();

            var commission = await _proxyContext.Commissions
                .FirstOrDefaultAsync(c => c.ServiceCode == ServiceCode);

            if (commission == null)
                return NotFound("委託不存在");

            if (commission.CreatorId != userId)
                return Forbid("系統錯誤，你不是此委託者");

            if (commission.Status != "已寄出")
                return BadRequest("目前狀態不可取消");

            var order = await _proxyContext.CommissionOrders
                .FirstOrDefaultAsync(o => o.CommissionId == commission.CommissionId);

            if (order == null || order.Status != "PENDING")
                return BadRequest("訂單紀錄不存在");

            var oldStatus = commission.Status; //紀錄舊狀態
            var cancelInfo = new
            {
                Amount = commission.EscrowAmount,
                to = commission.CreatorId,
            };


            // 狀態
            commission.Status = "cancelled";
            commission.UpdatedAt = DateTime.Now;
            order.Status = "CANCELLED";
            order.FinishedAt = DateTime.Now;

            // 退款
            var paymentService = new CommissionPaymentService(_proxyContext);
            await paymentService.RefundToBuyerAsync(commission.CommissionId);

            // History
            var oldDiff = new Dictionary<string, object>();
            var newDiff = new Dictionary<string, object>();

            if (oldStatus != "cancelled" && oldStatus == "已寄出")
            {
                oldDiff["status"] = oldStatus;
                newDiff["status"] = commission.Status;
            }
            if (dto.Reason != null)
            {
                oldDiff["Reason"] = null;
                newDiff["Reason"] = dto.Reason;
            }
            newDiff["cancelAmount"] = cancelInfo;

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            _proxyContext.CommissionHistories.Add(new CommissionHistory
            {
                CommissionId = commission.CommissionId,
                Action = "CANCEL_COMMISSION",
                ChangedBy = userId,
                OldData = JsonSerializer.Serialize(oldDiff, jsonOptions),
                NewData = JsonSerializer.Serialize(newDiff, jsonOptions)
            });

            await _proxyContext.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new { success = true, message = "訂單已取消並退款" });
        }
        
        // 刪除委託-> done 
        [HttpDelete("{serviceCode}")]
        public async Task<IActionResult> DeleteCommission(string serviceCode)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            using var transaction = await _proxyContext.Database.BeginTransactionAsync();
            try
            {
                // 1. 抓取委託時，順便把「歷史紀錄」和「地點」一起抓出來
                var commission = await _proxyContext.Commissions
                    .Include(c => c.Place)
                    .Include(c => c.CommissionHistories) // ✨ 新增這行：抓出歷史紀錄
                    .FirstOrDefaultAsync(c => c.ServiceCode == serviceCode && c.CreatorId == userId);

                if (commission == null) return NotFound(new { success = false, message = "找不到委託 (´;ω;`)" });

                // 2. 退款邏輯 (維持不變)
                var user = await _proxyContext.Users.FirstOrDefaultAsync(u => u.Uid == userId);
                if (user != null && commission.EscrowAmount.HasValue)
                {
                    decimal refundAmount = commission.EscrowAmount.Value;
    
                    // 1. 更新使用者餘額
                    user.Balance += refundAmount;

                    // 2. ✨ 新增錢包日誌紀錄
                    var walletLog = new WalletLog
                    {
                        Uid = userId!,
                        Action = "CommissionDelete", // 讓前端可以判斷這是刪除退款
                        Amount = refundAmount,
                        Balance = user.Balance??0m,       // 紀錄退款後的身家財產
                        EscrowBalance = 0m,            // 該筆交易已結束，押金變動設為 0
                        CreatedAt = DateTime.Now,
                        ServiceCode = commission.ServiceCode,
                        Description = commission.Title,
                    };

                    _proxyContext.WalletLogs.Add(walletLog);
                }

                // 3. ✨ 先刪除「歷史紀錄」
                if (commission.CommissionHistories.Any())
                {
                    _proxyContext.CommissionHistories.RemoveRange(commission.CommissionHistories);
                }

                // 4. 刪除「地點」與「委託主體」
                var relatedPlace = commission.Place;
                _proxyContext.Commissions.Remove(commission);
        
                if (relatedPlace != null)
                {
                    _proxyContext.CommissionPlaces.Remove(relatedPlace);
                }

                await _proxyContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "全部清理乾淨囉！金額也退回了✨" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // 如果還是失敗，我們可以看更詳細的錯誤：ex.InnerException?.Message
                return StatusCode(500, new { success = false, message = "刪除失敗", error = ex.InnerException?.Message ?? ex.Message });
            }
        }

    }
}
