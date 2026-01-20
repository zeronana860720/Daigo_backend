using DemoShopApi.DTOs;
using DemoShopApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DemoShopApi.Validation;
using DemoShopApi.Data;
using Microsoft.AspNetCore.Http.HttpResults;

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
            using var transaction = await _ProxyContext.Database.BeginTransactionAsync();

            try
            {
                var Commission = await _ProxyContext.Commissions
                                        .FirstOrDefaultAsync(p => p.CommissionId == commissionId && p.CreatorId == userId); //驗證是不是訂單的建單人，跟委託是不是跟資料庫的是同一筆
                if (Commission == null)
                {
                    return (false, "找不到此委託");
                }

                if (Commission.Status != "審核中" && Commission.Status != "審核失敗")
                {
                    return (false, "此狀態不可編輯");
                }

                var user = await _ProxyContext.Users
                                   .FirstOrDefaultAsync(u => u.Uid == userId);
                if (user == null)
                    return (false, "使用者不存在");



               

                var oldDiff = new Dictionary<string, object>();
                var newDiff = new Dictionary<string, object>();

                
                if (Commission.Title != dto.Title)
                {
                    oldDiff["Title"] = Commission.Title;
                    newDiff["Title"] = dto.Title;
                }
                if (Commission.Description != dto.Description)
                {
                    oldDiff["Description"] = Commission.Description;
                    newDiff["Description"] = dto.Description;
                }
                if (Commission.Price != dto.Price)
                {
                    oldDiff["Price"] = Commission.Price; 
                    newDiff["Price"] = dto.Price;
                }
                if (Commission.Quantity != dto.Quantity)
                {
                    oldDiff["Quantity "] = Commission.Quantity;
                    newDiff["Quantity "] = dto.Quantity;
                }
                if (Commission.Category != dto.Category)
                {
                    oldDiff["Category "] = Commission.Category;
                    newDiff["Category "] = dto.Category;
                }
                if (Commission.Deadline != dto.Deadline)
                {
                    oldDiff["Deadline "] = Commission.Deadline;
                    newDiff["Deadline "] = dto.Deadline.AddDays(7);
                }
                if (Commission.Location != dto.Location)
                {
                    oldDiff["Location "] = Commission.Location;
                    newDiff["Location "] = dto.Location;
                }

                var oldEscrow = Commission.EscrowAmount;

                //金額被修改 ->重算
                decimal feeRate = 0.1m;
                decimal newfee = (dto.Price * dto.Quantity) * feeRate; //新的手續費用
                decimal newtotal = Math.Round((dto.Price * dto.Quantity) + newfee
                                                    , 0, MidpointRounding.AwayFromZero);

                var diff = newtotal - oldEscrow; //金額差異
                if (diff > 0 && user.Balance < diff)
                {
                    return (false, "錢包餘額不足，金額變更失敗");
                }
                user.Balance -= diff;   

                Commission.Title = dto.Title;
                Commission.Description = dto.Description;
                Commission.Price = dto.Price;
                Commission.Quantity = dto.Quantity;
                Commission.Category = dto.Category;
                Commission.Location = dto.Location;
                if (Commission.Deadline != dto.Deadline)
                {
                    Commission.Deadline = dto.Deadline.AddDays(7);
                } 
                Commission.Fee = newfee;
                Commission.EscrowAmount = newtotal;

                Commission.Status = "審核中"; //編輯過都要重新審核

                //圖片處理
                if (dto.Image != null && dto.Image.Length > 0)
                {
                    var uploadPath = Path.Combine("wwwroot", "uploads");  
                    Directory.CreateDirectory(uploadPath);                                 

                    if (!string.IsNullOrEmpty(Commission.ImageUrl))
                    {
                        var oldImagePath = Path.Combine("wwwroot", Commission.ImageUrl.TrimStart('/')); 
                        if (File.Exists(oldImagePath)) 
                        {
                            File.Delete(oldImagePath);
                        }
                    }
                  
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.Image.FileName)}"; 
                    var filePath = Path.Combine(uploadPath, fileName);
                                                                    
                    using var stream = new FileStream(filePath, FileMode.Create); 
                    await dto.Image.CopyToAsync(stream); 

                    Commission.ImageUrl = $"/uploads/{fileName}";
                }


              
                var jsonOptions = new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                if (oldDiff.Any())
                {
                    var history = new CommissionHistory
                    {
                        CommissionId = Commission.CommissionId,
                        Action = "EDIT",
                        ChangedBy = userId,
                        ChangedAt = DateTime.Now,
                        OldData = JsonSerializer.Serialize(oldDiff, jsonOptions),
                        NewData = JsonSerializer.Serialize(newDiff, jsonOptions)
                    };


                    _ProxyContext.CommissionHistories.Add(history);
                }




                await _ProxyContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, "委託更改成功，狀態退回審核中");
            }
            catch
            {
                await transaction.RollbackAsync();
                return (false, "系統發生錯誤，請稍後再試");
            }
            //catch (Exception ex)
            //{
            //    await transaction.RollbackAsync();
            //    return 
            //    (
            //        false,
            //        ex.Message
                    
            //    );
            //}
        }
    }
}
