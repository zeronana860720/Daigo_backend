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
        [Authorize]
[HttpPost("Create")]
public async Task<IActionResult> CreateCommission([FromForm] CommissionCreateDto dto)
{
    // 1. 驗證資料格式
    if (!ModelState.IsValid)
    {
        return BadRequest(new
        {
            success = false,
            errors = ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .ToDictionary(k => k.Key, v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray())
        });
    }

    // 2. 取得使用者資訊 (從 JWT Token 抓取)
    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrEmpty(userId))
    {
        return Unauthorized(new { success = false, message = "無效的驗證資訊，請重新登入！" });
    }

    var user = await _proxyContext.Users.FirstOrDefaultAsync(u => u.Uid == userId);

    if (user == null)
    {
        return Unauthorized(new { success = false, message = "找不到該使用者，請重新登入！" });
    }

    // 3. 後端安全匯率計算
    var rates = new Dictionary<string, decimal> {
        { "JPY", 0.201m }, { "TWD", 1.0m }, { "USD", 32.5m }, { "EUR", 35.2m }, { "KRW", 0.024m }
    };
    decimal currentRate = rates.ContainsKey(dto.Currency ?? "TWD") ? rates[dto.Currency!] : 1.0m;

    decimal subtotalTwd = (dto.Price * dto.Quantity) * currentRate;
    decimal feeRate = 0.1m; 
    decimal priceFeeTwd = Math.Round(subtotalTwd * feeRate, 0, MidpointRounding.AwayFromZero);
    decimal totalPriceTwd = Math.Round(subtotalTwd + priceFeeTwd, 0, MidpointRounding.AwayFromZero);

    // 4. 餘額檢查
    if (user.Balance < totalPriceTwd)
    {
        return BadRequest(new { success = false, code = "BALANCE_NOT_ENOUGH", message = "錢包餘額不足" });
    }

    // 5. 資料庫交易開始
    using var transaction = await _proxyContext.Database.BeginTransactionAsync();
    try
    {
        user.Balance -= totalPriceTwd; // 扣錢

        // 圖片路徑處理
        string? imageUrl = null;
        string? absolutePath = null;
        if (dto.Image != null && dto.Image.Length > 0)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
            absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
            imageUrl = $"/uploads/{fileName}";
        }

        // 6. 建立委託實體 (注意 Place 的層級)
        var commission = new Commission
        {
            CreatorId = userId,
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            Location = dto.Location,
            Price = dto.Price,
            Currency = dto.Currency ?? "TWD",
            Fee = priceFeeTwd,
            EscrowAmount = totalPriceTwd,
            Quantity = dto.Quantity,
            Deadline = dto.Deadline.AddDays(7),
            Status = "審核中",
            CreatedAt = DateTime.Now,
            ImageUrl = imageUrl,

            // ✨ 這裡就是解決紅字的關鍵！
            // 不要直接寫 GooglePlaceId = dto.google_place_id
            // 必須包在 new CommissionPlace 裡面
            Place = new CommissionPlace
            {
                GooglePlaceId = dto.google_place_id ?? "",
                FormattedAddress = dto.formatted_address ?? "",
                Latitude = dto.latitude ?? 0m,
                Longitude = dto.longitude ?? 0m,
                CreatedAt = DateTime.Now
            }
        };

        await _CreateCode.CreateCommissionCodeAsync(commission);
        _proxyContext.Commissions.Add(commission);
        await _proxyContext.SaveChangesAsync();

        // 7. 記錄歷史
        var jsonOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var history = new CommissionHistory
        {
            CommissionId = commission.CommissionId,
            Action = "CREATE",
            ChangedBy = userId,
            ChangedAt = DateTime.Now,
            NewData = JsonSerializer.Serialize(new
            {
                commission.Title,
                commission.Price,
                commission.Currency,
                commission.Quantity,
                TwdTotal = totalPriceTwd,
                FormattedAddress = commission.Place?.FormattedAddress
            }, jsonOptions)
        };
        _proxyContext.CommissionHistories.Add(history);
        await _proxyContext.SaveChangesAsync();

        // 8. 儲存檔案
        if (dto.Image != null && absolutePath != null)
        {
            var folder = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder!);
            using var stream = new FileStream(absolutePath, FileMode.Create);
            await dto.Image.CopyToAsync(stream);
        }

        await transaction.CommitAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                serviceCode = commission.ServiceCode,
                originalPrice = commission.Price,
                currency = commission.Currency,
                totalPriceTwd = commission.EscrowAmount,
                status = commission.Status,
                formattedAddress = commission.Place?.FormattedAddress
            }
        });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return StatusCode(500, new { success = false, message = "建立委託失敗", error = ex.Message });
    }
}

        //編輯委託
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
            var userid = "101";

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

        //接受委託
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
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? "102";// 接單者
            var commissionId = await _proxyContext.Commissions
                                                 .Where(c => c.ServiceCode == ServiceCode)
                                                 .Select(c => c.CommissionId)
                                                 .FirstOrDefaultAsync();
            if (commissionId == 0)
            {
                return NotFound("委託不存在");
            }
            using var tx = await _proxyContext.Database.BeginTransactionAsync();

            var order = await _proxyContext.CommissionOrders
                .FirstOrDefaultAsync(o => o.CommissionId == commissionId && o.SellerId == userId);

            if (order == null)
                return Forbid("你不是接單者");

            var commission = await _proxyContext.Commissions
                .FirstOrDefaultAsync(c => c.CommissionId == commissionId);
            if (commission == null)
            {
                return NotFound("委託不存在");
            }
            if (dto.Image == null) { return BadRequest("請上傳圖片"); }

            if (commission.Status != "已接單" && commission.Status != "出貨中")
                return BadRequest("目前狀態不可上傳明細");


            var commissionReceipt = await _proxyContext.CommissionReceipts
                                   .FirstOrDefaultAsync(c => c.CommissionId == commissionId);
            bool isFirstUpload = commissionReceipt == null;
            var oldremark = commissionReceipt?.Remark;


            // 存圖片
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
            var path = Path.Combine("wwwroot", "receipts", fileName);
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

            // 不管是不是第一次，都是更新「同一筆」
            commissionReceipt.ReceiptImageUrl = newImageUrl;
            commissionReceipt.ReceiptAmount = dto.ReceiptAmount;
            commissionReceipt.ReceiptDate = dto.ReceiptDate;
            commissionReceipt.Remark = dto.Remark;

            var oldStatus = commission.Status;
            if (commission.Status == "已接單")
            {
                commission.Status = "出貨中";
                commission.UpdatedAt = DateTime.Now;
            }


            var oldDiff = new Dictionary<string, object>();
            var newDiff = new Dictionary<string, object>();


            oldDiff["imageurl"] = (isFirstUpload == true ? "null" : commissionReceipt.ReceiptImageUrl);
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


        //寄貨後按鈕
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

        //完成訂單 (買家)
        [HttpPost("{ServiceCode}/complete")]
        public async Task<IActionResult> CompleteCommission(string ServiceCode)
        {
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "101";

            using var tx = await _proxyContext.Database.BeginTransactionAsync();

            var commission = await _proxyContext.Commissions
                .FirstOrDefaultAsync(c => c.ServiceCode == ServiceCode);

            if (commission == null)
                return NotFound("委託不存在");

            if (commission.CreatorId != userId)
                return Forbid("你不是此委託的建立者");

            if (commission.Status != "已寄出")
                return BadRequest("目前狀態不可完成");

            var order = await _proxyContext.CommissionOrders
                .FirstOrDefaultAsync(o => o.CommissionId == commission.CommissionId);

            if (order == null || order.Status != "PENDING")
                return BadRequest("訂單紀錄不存在或訂單尚未完成寄貨");

            var oldStatus = commission.Status; //已寄出 狀態紀錄
            var paymentInfo = new
            {
                orderAmount = order.Amount,
                fee = commission.Fee,
                releaseToSeller = order.Amount - commission.Fee
            };

            // 狀態更新
            commission.Status = "已完成";
            commission.UpdatedAt = DateTime.Now;
            order.Status = "COMPLETED";
            order.FinishedAt = DateTime.Now;

            // 金流
            var paymentService = new CommissionPaymentService(_proxyContext);
            await paymentService.ReleaseToSellerAsync(commission.CommissionId);

            // History
            var oldDiff = new Dictionary<string, object>();
            var newDiff = new Dictionary<string, object>();
            if (oldStatus != commission.Status)
            {
                oldDiff["status"] = oldStatus;
                newDiff["status"] = commission.Status;
            }
            newDiff["payment"] = paymentInfo;



            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
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

    }
}
