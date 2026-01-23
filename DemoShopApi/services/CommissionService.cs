using DemoShopApi.DTOs;
using DemoShopApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DemoShopApi.Data;

namespace DemoShopApi.services
{
    public class CommissionService
    {
        private readonly DaigoContext _ProxyContext;

        public CommissionService(DaigoContext proxycontext)
        {
            _ProxyContext = proxycontext;
        }

        public async Task<(bool success, string message)> EditCommissionAsync(int commissionId, string userId, CommissionEditDto dto)
        {
            // 使用資料庫交易，確保所有步驟（扣款、更新、紀錄）要麼全部成功，要麼全部失敗
            using var transaction = await _ProxyContext.Database.BeginTransactionAsync();

            try
            {
                // 1. 基礎驗證：檢查委託是否存在，且是否為本人建立
                var commission = await _ProxyContext.Commissions
                    .FirstOrDefaultAsync(p => p.CommissionId == commissionId && p.CreatorId == userId);

                if (commission == null) return (false, "找不到此委託或權限不足");

                // 2. 狀態檢查：只有「審核中」或「失敗」可以編輯
                if (commission.Status != "審核中" && commission.Status != "審核失敗")
                    return (false, "目前狀態不可編輯喔！");

                var user = await _ProxyContext.Users.FirstOrDefaultAsync(u => u.Uid == userId);
                if (user == null) return (false, "使用者不存在");

                // 3. 紀錄變更前與變更後的差異 (用於歷史紀錄)
                var oldDiff = new Dictionary<string, object>();
                var newDiff = new Dictionary<string, object>();

                void CheckChange(string field, object? oldVal, object? newVal)
                {
                    if (oldVal?.ToString() != newVal?.ToString())
                    {
                        oldDiff[field] = oldVal ?? "null";
                        newDiff[field] = newVal ?? "null";
                    }
                }

                CheckChange("Title", commission.Title, dto.Title);
                CheckChange("Description", commission.Description, dto.Description);
                CheckChange("Price", commission.Price, dto.Price);
                CheckChange("Quantity", commission.Quantity, dto.Quantity);
                CheckChange("Category", commission.Category, dto.Category);
                CheckChange("Currency", commission.Currency, dto.Currency);
                CheckChange("Location", commission.Location, dto.Location);

                // 4. 金額重新計算與扣款邏輯
                decimal feeRate = 0.1m; // 10% 手續費
                decimal newFee = (dto.Price * dto.Quantity) * feeRate;
                decimal newTotal = Math.Round((dto.Price * dto.Quantity) + newFee, 0, MidpointRounding.AwayFromZero);

                // 計算差額 (新總額 - 舊的押金)
                var diff = newTotal - (commission.EscrowAmount ?? 0);
                if (diff > 0 && user.Balance < diff) return (false, "錢包餘額不足，無法支付差額喔 (´;ω;`) ");
                
                user.Balance -= diff; // 扣除(或退回)差額

                // 5. 更新委託基本資料
                commission.Title = dto.Title;
                commission.Description = dto.Description;
                commission.Price = dto.Price;
                commission.Quantity = dto.Quantity;
                commission.Category = dto.Category;
                commission.Currency = dto.Currency;
                commission.Location = dto.Location;
                commission.Fee = newFee;
                commission.EscrowAmount = newTotal;
                commission.Status = "審核中"; // 編輯後需重新審核
                commission.UpdatedAt = DateTime.Now;

                // 處理日期：若有變動則更新 (依妳的要求 AddDays(7))
                if (commission.Deadline != dto.Deadline)
                {
                    commission.Deadline = dto.Deadline.AddDays(7);
                    CheckChange("Deadline", commission.Deadline, dto.Deadline.AddDays(7));
                }

                // 6. 地點關聯處理 (CommissionPlace)
                if (!string.IsNullOrEmpty(dto.GooglePlaceId))
                {
                    var existingPlace = await _ProxyContext.CommissionPlaces
                        .FirstOrDefaultAsync(p => p.GooglePlaceId == dto.GooglePlaceId);

                    if (existingPlace != null)
                    {
                        commission.PlaceId = existingPlace.PlaceId;
                    }
                    else
                    {
                        var newPlace = new CommissionPlace
                        {
                            GooglePlaceId = dto.GooglePlaceId,
                            Name = dto.Location,
                            FormattedAddress = dto.FormattedAddress ?? "",
                            Latitude = dto.Latitude ?? 0,
                            Longitude = dto.Longitude ?? 0,
                            CreatedAt = DateTime.Now
                        };
                        _ProxyContext.CommissionPlaces.Add(newPlace);
                        await _ProxyContext.SaveChangesAsync(); // 取得新 ID
                        commission.PlaceId = newPlace.PlaceId;
                    }
                }

                // 7. 圖片處理：刪除舊圖存入新圖
                if (dto.Image != null && dto.Image.Length > 0)
                {
                    var uploadPath = Path.Combine("wwwroot", "uploads");
                    Directory.CreateDirectory(uploadPath);

                    if (!string.IsNullOrEmpty(commission.ImageUrl))
                    {
                        var oldImagePath = Path.Combine("wwwroot", commission.ImageUrl.TrimStart('/'));
                        if (File.Exists(oldImagePath)) File.Delete(oldImagePath);
                    }

                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}";
                    var filePath = Path.Combine(uploadPath, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await dto.Image.CopyToAsync(stream);

                    commission.ImageUrl = $"/uploads/{fileName}";
                }

                // 8. 儲存歷史紀錄
                if (oldDiff.Any())
                {
                    var jsonOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                    _ProxyContext.CommissionHistories.Add(new CommissionHistory
                    {
                        CommissionId = commission.CommissionId,
                        Action = "EDIT",
                        ChangedBy = userId,
                        ChangedAt = DateTime.Now,
                        OldData = JsonSerializer.Serialize(oldDiff, jsonOptions),
                        NewData = JsonSerializer.Serialize(newDiff, jsonOptions)
                    });
                }

                await _ProxyContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, "委託更改成功，已送交重新審核！");
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return (false, "系統發生錯誤，請稍後再試 (＞x＜)");
            }
        }
    }
}