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
        // done
        [HttpGet]
        public async Task<IActionResult> GetCommissionsList() 
        { 
            // 1️⃣ 先從資料庫抓出所有「待接單」的委託
            var commissions = await _proxyContext.Commissions
                .Where(u => u.Status == "待接單")
                .Select(u => new 
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
                    u.Fee
                })
                .ToListAsync();  // ✨ 先 ToListAsync() 把資料拉到記憶體

            // 2️⃣ 定義匯率
            var rates = new Dictionary<string, decimal> 
            { 
                { "JPY", 0.201m }, 
                { "TWD", 1.0m }, 
                { "USD", 32.5m } 
            };

            // 3️⃣ 在記憶體中計算報酬率並排序
            var result = commissions
                .Select(u => new
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
                    u.Fee,
                    // ✨ 計算報酬率: Fee / (Price * 匯率) * 100
                    FeeRate = u.Price > 0 
                        ? (u.Fee / (u.Price * (rates.ContainsKey(u.Currency) ? rates[u.Currency] : 1.0m))) * 100 
                        : 0
                })
                .OrderByDescending(u => u.FeeRate)  // 按報酬率由高到低排序
                .ToList();

            return Ok(new
            {
                success = true,
                data = result
            });
        }



        //點擊委託之後 顯示的單筆詳細資料
        //done
        [HttpGet("{ServiceCode}")]
        public async Task<IActionResult> GetDetail(string ServiceCode) 
        {
            var Commission = await _proxyContext.Commissions
                                               .Where(c => c.ServiceCode == ServiceCode && c.Status == "待接單")
                                               .Select(c => new
                                               {    //比普通清單多
                                                   c.ServiceCode, // 流水號
                                                   c.CreatorId,
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
                                                   c.Currency,
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
        
        // 獲取熱門委託 (首頁用)
        [HttpGet("Hot")]
        public async Task<IActionResult> GetHotCommissions() 
        { 
            // 1.定義匯率
            var rates = new Dictionary<string, decimal> 
            { 
                { "JPY", 0.201m }, 
                { "TWD", 1.0m }, 
                { "USD", 32.5m } 
            };

            // 2.先按 Fee (總報酬) 由高到低排序,取前 10 個
            var topByFee = await _proxyContext.Commissions
                .Where(u => u.Status == "待接單")
                .OrderByDescending(u => u.Fee)
                .Take(10)
                .Select(u => new 
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
                    u.Fee
                })
                .ToListAsync();

            // 3.從這 10 個計算報酬率並排序,取前 4 個
            var result = topByFee
                .Select(u => new
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
                    u.Fee,
                    // 計算報酬率: Fee / (Price * 匯率) * 100
                    FeeRate = u.Price > 0 
                        ? (u.Fee / (u.Price * (rates.ContainsKey(u.Currency) ? rates[u.Currency] : 1.0m))) * 100 
                        : 0
                })
                .OrderByDescending(u => u.FeeRate)
                .Take(4)
                .ToList();

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        // 新增：查看委託收據的 API
        // GET: api/Commissions/{serviceCode}/receipt
        [HttpGet("{serviceCode}/receipt")]
        public async Task<IActionResult> GetReceipt(string serviceCode)
        {
            // 1. 搜尋收據資料表
            // 假設你的 Context 裡面有 CommissionReceipts 這個 DbSet
            // 我們透過 ServiceCode 來連結 Commission 資料表確認是哪筆訂單
            var receipt = await _proxyContext.CommissionReceipts
                .Where(r => r.Commission.ServiceCode == serviceCode)
                .Select(r => new
                {
                    r.ReceiptId,
                    r.ReceiptImageUrl, // 收據圖片
                    r.ReceiptAmount,   // 收據金額
                    r.ReceiptDate,     // 收據上的日期
                    r.Remark,          // 備註
                    r.UploadedAt       // 上傳時間
                })
                .FirstOrDefaultAsync();

            // 2. 如果找不到收據（可能還沒上傳，或是單號錯了）
            if (receipt == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "找不到此委託的收據資料 (｡•́︿•̀｡)"
                });
            }

            // 3. 成功找到就回傳
            return Ok(new
            {
                success = true,
                data = receipt
            });
        }
        


        

    }
}
