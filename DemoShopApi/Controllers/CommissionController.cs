using DemoShopApi.DTOs;
using DemoShopApi.Models;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using DemoShopApi.Data;
using DemoShopApi.services;

namespace DemoShopApi.Controllers
{
    [ApiController]
    [Route("api/Commissions")]
    public class CommissionController : ControllerBase
    {
        private readonly DaigoContext _proxyContext;
        private readonly CommissionService _CommissionService;
        private readonly CreateCommissionCode _CreateCode;
        public CommissionController(DaigoContext proxyContext, CommissionService commissionService, CreateCommissionCode CreateCode)
        {
            _proxyContext = proxyContext;
            _CommissionService = commissionService;
            _CreateCode = CreateCode;
        }

        //委託 待接單所有 展示
        [HttpGet]
        public async Task<IActionResult> GetCommissionsList() 
        { 
            var commissions = await _proxyContext.Commissions
                                                .Where(u=>u.Status == "待接單")
                                                .OrderBy(u=>u.Deadline) //由大到小時間抓
                                                .Select(u=> new 
                                                { 
                                                u.ServiceCode,
                                                u.Title,
                                                u.Price,
                                                u.Quantity,
                                                u.Location,
                                                u.Category,
                                                u.ImageUrl,
                                                u.Deadline,
                                                u.Status,
                                                u.Currency,
                                                }).ToListAsync();

            return Ok(new
            {
                success = true,
                data = commissions
            });
        }

        //點擊委託之後 顯示的單筆詳細資料
        [HttpGet("{ServiceCode}")]
        public async Task<IActionResult> GetDetail(string ServiceCode) 
        {
            var Commission = await _proxyContext.Commissions
                                               .Where(c => c.ServiceCode == ServiceCode && c.Status == "待接單")
                                               .Select(c => new
                                               {    //比普通清單多
                                                   c.ServiceCode, // 流水號
                                                   c.Title, // 標題
                                                   c.Description, //描述
                                                   c.Price, // 價格
                                                   c.Quantity,  // 數量
                                                   c.Fee,       //平台手續費
                                                   c.EscrowAmount, // 會拿到的總價格
                                                   c.Category,      // 商品分類
                                                   c.Location,      // 地點-> 可以不用
                                                   c.ImageUrl,     // 圖片url
                                                   c.CreatedAt, //這委託建立的時間
                                                   c.Deadline,  // 截止時間
                                                   c.Status,       // 狀態
                                                   // ✨ 關鍵改動：透過 Place 導覽屬性抓取經緯度
                                                   // 假設你在 C# Model 中將該關聯屬性命名為 Place
                                                   Latitude = c.Place != null ? c.Place.Latitude : (decimal?)null,
                                                   Longitude = c.Place != null ? c.Place.Longitude : (decimal?)null,
                                                   name = c.Place != null ? c.Place.Name : null,
                                                   // 也可以順便抓取格式化地址
                                                   FullAddress = c.Place != null ? c.Place.FormattedAddress : c.Location,
                                                   MapUrl = c.Place != null ? c.Place.MapUrl : null
                                               }).FirstOrDefaultAsync();
            if (Commission==null) {
                return NotFound(
                            new
                            {
                                success = false,
                                message = "找不到此委託"
                            } );
            }
            return Ok(new
            {
                success = true,
                data = Commission
            });
        
        }

        


        

    }
}
