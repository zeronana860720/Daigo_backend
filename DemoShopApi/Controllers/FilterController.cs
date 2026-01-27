using DemoShopApi.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DemoShopApi.Models;
using Microsoft.EntityFrameworkCore;
namespace API大專.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilterController : ControllerBase
    {
        private DaigoContext _context;

        public FilterController(DaigoContext context)
        {
            _context = context;
        }

        // 根據關鍵字搜尋委託單 (搜尋 Title, Description, Category, Location)
        [HttpGet("search")]
        public IActionResult SearchCommissions(
            [FromQuery] string? keyword,
            [FromQuery] string? location,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] string? sort = null
        )
        {
            // 1. 取得基礎查詢，並使用 Include 預先載入地點資料表
            // 註：請確保 Commission Model 裡有 public virtual CommissionPlace? Place { get; set; } 這樣的導覽屬性
            var query = _context.Commissions
                .Include(c => c.Place) // ✨ 連接到 Commission_Place 表
                .Where(c => c.Status == "待接單");

            // 2. 關鍵字搜尋 (同步更新：關鍵字也能搜到地址)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(c =>
                    (c.Title != null && c.Title.Contains(keyword)) ||
                    (c.Description != null && c.Description.Contains(keyword)) ||
                    (c.Category != null && c.Category.Contains(keyword)) ||
                    // ✨ 修改：關鍵字現在也比對 Commission_Place 的地址
                    (c.Place != null && c.Place.FormattedAddress.Contains(keyword))
                );
            }

            // 3. 地點篩選 (核心修改：依照 Commission_Place 的 formatted_address 搜尋)
            if (!string.IsNullOrWhiteSpace(location))
            {
                // ✨ 修改：不再比對 c.Location，而是比對關聯表的地址
                query = query.Where(c => c.Place != null && c.Place.FormattedAddress.Contains(location));
            }

            // 4. 價格篩選 (維持不變)
            if (minPrice.HasValue) query = query.Where(c => c.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(c => c.Price <= maxPrice.Value);

            // 5. 多重排序邏輯 (維持不變)
            var sortOrders = sort?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            IOrderedQueryable<Commission>? orderedQuery = null;

            foreach (var s in sortOrders)
            {
                var action = s.Trim().ToLower();
                bool isFirst = (orderedQuery == null);
                switch (action)
                {
                    case "price_asc":
                        orderedQuery = isFirst ? query.OrderBy(c => c.Price) : orderedQuery!.ThenBy(c => c.Price);
                        break;
                    case "price_desc":
                        orderedQuery = isFirst ? query.OrderByDescending(c => c.Price) : orderedQuery!.ThenByDescending(c => c.Price);
                        break;
                    case "deadline_asc":
                        orderedQuery = isFirst ? query.OrderBy(c => (c.Deadline ?? DateTime.MaxValue).Date) : orderedQuery!.ThenBy(c => (c.Deadline ?? DateTime.MaxValue).Date);
                        break;
                    case "deadline_desc":
                        orderedQuery = isFirst ? query.OrderByDescending(c => (c.Deadline ?? DateTime.MinValue).Date) : orderedQuery!.ThenBy(c => (c.Deadline ?? DateTime.MinValue).Date);
                        break;
                }
            }

            var finalQuery = orderedQuery ?? query.OrderByDescending(c => c.CreatedAt);

            // 6. 最終投影 (將地址資訊也丟給前端)
            var results = finalQuery.Select(c => new
            {
                c.Title,
                c.Price,
                c.Quantity,
                // ✨ 修改：將關聯表的 formatted_address 當作 location 傳回前端
                Location = c.Place != null ? c.Place.FormattedAddress : c.Location,
                c.Category,
                c.ImageUrl,
                c.Deadline,
                c.Description,
                c.Currency,
                c.ServiceCode
            }).Take(50).ToList();

            return Ok(new { success = true, count = results.Count, data = results });
        }



        // 根據地點(location)篩選委託單，並可依價格或截止日期排序
        [HttpGet("location/{location}")]
        public IActionResult GetByLocation(
            string location, 
            [FromQuery] string? sort = null  )
        { 
            // 根據location參數過濾
            var query = _context.Commissions                     
                                  .Where(c => c.Location != null && c.Location.Contains(location) && c.Status == "待接單");

            // 解析排序字串 (支援多個條件，由逗號隔開)，例如：sort = "deadline_asc,price_desc"
            //將前端傳進來的排序字串（例如 "price_desc,deadline_asc"）安全地拆解成一個一個的排序指令陣列
            //.Split(',')： 告訴程式以「逗號」作為分界點
            //StringSplitOptions.RemoveEmptyEntries「防呆」如果使用者不小心輸入了 price_desc,,deadline_asc（多一個逗號），會自動忽略掉中間空的內容
            //「??」左邊如果是空的，就取右邊
            var sortOrders = sort?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            //執行.OrderBy() 後，回傳的類型會從 IQueryable 變成 IOrderedQueryable
            //只有 IOrderedQueryable 類型才能接著呼叫.ThenBy()（次要排序）。
            IOrderedQueryable<Commission>? orderedQuery = null;

            foreach (var s in sortOrders)
            {
                var action = s.Trim().ToLower();
                bool isFirst = (orderedQuery == null);

                // 根據是否為第一個條件，決定使用 OrderBy 或 ThenBy
                switch (action)
                {
                    case "price_asc":
                        orderedQuery = isFirst ? query.OrderBy(c => c.Price) : orderedQuery!.ThenBy(c => c.Price);
                        break;
                    case "price_desc":
                        orderedQuery = isFirst ? query.OrderByDescending(c => c.Price) : orderedQuery!.ThenByDescending(c => c.Price);
                        break;
                    case "deadline_asc":
                        // 因為deadline細到時間，使用 .Value.Date 只針對日期排序
                        orderedQuery = isFirst 
                            ? query.OrderBy(c => c.Deadline.Value.Date)
                            : orderedQuery!.ThenBy(c => c.Deadline.Value.Date);
                        break;
                    case "deadline_desc":
                        orderedQuery = isFirst 
                            ? query.OrderByDescending(c => c.Deadline.Value.Date) 
                            : orderedQuery!.ThenByDescending(c => c.Deadline.Value.Date);
                        break;
                }
            }

            // 3. 如果完全沒有排序參數，則使用預設排序 (建立時間)
            var finalQuery = orderedQuery ?? query.OrderByDescending(c => c.CreatedAt);

            var results = finalQuery.Select(c => new
            {
                c.Title,
                c.Price,
                c.Quantity,
                c.Location,
                c.Category,
                c.ImageUrl,
                c.Deadline,
                c.Description
            }).Take(30)
            .ToList();
            
            return Ok(new
            {
                success = true,
                data = results
            });
            }

        // 取得前五名熱門地點
        [HttpGet("top5-locations")]
            public IActionResult GetTopLocations()
            {
                var topLocations = _context.Commissions
                    // 1. 依照地點分組
                    .GroupBy(c => c.Location)
                    // 2. 轉換成匿名物件，包含地點名稱與該地點的筆數
                    .Select(g => new
                    {
                        Location = g.Key,
                        Count = g.Count()
                    })
                    // 3. 依照筆數由高到低排序
                    .OrderByDescending(x => x.Count)
                    // 4. 只取前五名
                    .Take(5)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = topLocations
                });
        }
    }
}
