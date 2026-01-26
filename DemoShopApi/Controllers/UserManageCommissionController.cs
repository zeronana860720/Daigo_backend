using Microsoft.AspNetCore.Mvc;
using DemoShopApi.DTOs;
using DemoShopApi.Models;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using DemoShopApi.Data;
using Microsoft.AspNetCore.Authorization;

namespace DemoShopApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Manage")]
    public class UserManageCommissionController : ControllerBase
    {
        private readonly DaigoContext _proxyContext;
        public UserManageCommissionController(DaigoContext proxyContext)
        {
            _proxyContext = proxyContext;
        }
        // Done
        [HttpGet("Commission")]
        public async Task<IActionResult> UserManage()
        {
            // 獲取目前登入的使用者 UID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "101";

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("尚未登入");
            }

            var commissions = await (from c in _proxyContext.Commissions
                // 1. 聯集地點表 (Left Join)
                join p in _proxyContext.CommissionPlaces on c.PlaceId equals p.PlaceId into ps
                from p in ps.DefaultIfEmpty() 
        
                // ✨ 新增邏輯：從 Review 表中找出這筆委託「最新的一筆」審核紀錄
                // 條件：類型是 commission 且 ID 對應，按時間由新到舊排序
                let latestReview = _proxyContext.Reviews
                    .Where(r => r.TargetType == "commission" && r.TargetId == c.CommissionId)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault()

                where c.CreatorId == userId && c.Status != "已完成" && c.Status != "cancelled"
        
                select new UserCommissionManageDto
                {
                    ServiceCode = c.ServiceCode,
                    Title = c.Title,
                    Status = c.Status,
                    Quantity = c.Quantity ?? 0,
                    Price = c.Price ?? 0,
                    Location = p.Name ?? "未設定地點", 
                    TotalAmount = ((c.Price ?? 0) * (c.Quantity ?? 0)) + (c.Fee ?? 0),
                    CreatedAt = c.CreatedAt,
                    EndAt = c.Deadline,
                    ImageUrl = c.ImageUrl,

                    // ✨ 新增欄位：將抓到的理由塞進 DTO，如果沒資料就給空字串
                    Reason = latestReview != null ? latestReview.Reason : "",

                    CanEdit = c.Status == "審核中" || c.Status == "審核失敗",
                    CanViewDetail = c.Status == "已出貨",
                    CanViewShipping = c.Status == "已寄出",
                    Currency = c.Currency,
                }).ToListAsync();

            return Ok(commissions);
        }
        // Done
        [HttpGet("Commission/MyAccept")]
        public async Task<IActionResult> AcceptManage()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? "102";// 接單者
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("尚未登入");
            }
       
            var order = await (
                        from o in _proxyContext.CommissionOrders
                        join p in _proxyContext.Commissions
                        on o.CommissionId equals p.CommissionId
                        where o.Status == "PENDING" && o.SellerId == userId
                        select new AcceptCommissionManageDto
                        {
                            ServiceCode = p.ServiceCode,
                            Title = p.Title,
                            Status = p.Status,
                            Quantity = p.Quantity??0,
                            Price = p.Price??0,
                            TotalAmount = ((p.Price??0) * (p.Quantity??0)),
                            Currency = p.Currency,  
                            PlatformFee = p.Fee??0,
                            CreatedAt = o.CreatedAt,
                            ImageUrl = p.ImageUrl,
                            CanUpdateReceipt = o.Status == "PENDING",
                            CanUpdateShip = o.Status == "PENDING" && p.Status =="出貨中",
                            CanViewReceipt = p.Status == "出貨中",//可以看自己上船的明細
                            CanViewShipping = p.Status == "已寄出"//可以看自己上傳的寄貨資訊
                        }
                        ).ToListAsync();

            return Ok(order);





        }

    }
}
