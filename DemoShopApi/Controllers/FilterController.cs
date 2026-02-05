using DemoShopApi.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DemoShopApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;

namespace API大專.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilterController : ControllerBase
    {
        private readonly DaigoContext _context;

        public FilterController(DaigoContext context)
        {
            _context = context;
        }

        // 根據關鍵字、地點(支援多重關鍵字)、價格範圍與排序來搜尋委託單
        [HttpGet("search")]
        public IActionResult SearchCommissions(
            [FromQuery] string? keyword,
            [FromQuery] string? location,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] string? sort = null
        )
        {
            // 1. 基礎查詢：預先載入地點資料表
            // 只篩選狀態為 "待接單" 的委託
            var query = _context.Commissions
                .Include(c => c.Place) // 連接到 Commission_Place 表
                .Where(c => c.Status == "待接單");

            // 2. 關鍵字全域搜尋
            // 搜尋範圍包含：標題、描述、分類以及地點的詳細地址
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(c =>
                    (c.Title != null && c.Title.Contains(keyword)) ||
                    (c.Description != null && c.Description.Contains(keyword)) ||
                    (c.Category != null && c.Category.Contains(keyword)) ||
                    (c.Place != null && c.Place.FormattedAddress.Contains(keyword))
                );
            }

            // 3. 地點篩選 (支援中英文多重關鍵字)
            // 前端傳入範例："osaka,大阪"
            if (!string.IsNullOrWhiteSpace(location))
            {
                // 將傳入的字串依照逗號切割成陣列，例如 ["osaka", "大阪"]
                // StringSplitOptions.RemoveEmptyEntries 防止出現空字串
                var locationKeywords = location.Split(',', StringSplitOptions.RemoveEmptyEntries);

                // 篩選邏輯：檢查 Commission 資料是否包含陣列中的「任何一個」關鍵字
                // 這會產生 OR 的效果 (包含 'osaka' 或者 包含 '大阪' 都算符合)
                query = query.Where(c => locationKeywords.Any(loc =>
                    (c.Location != null && c.Location.Contains(loc)) ||              // 比對手動輸入的地點欄位
                    (c.Place != null && c.Place.Name.Contains(loc)) ||               // 比對 Google Place 名稱
                    (c.Place != null && c.Place.FormattedAddress.Contains(loc))      // 比對 Google Place 完整地址
                ));
            }

            // 4. 價格區間篩選
            if (minPrice.HasValue)
                query = query.Where(c => c.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(c => c.Price <= maxPrice.Value);

            // 5. 排序邏輯
            // 解析排序字串，例如 "fee_rate_desc,price_asc"
            var sortOrders = sort?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            IOrderedQueryable<Commission>? orderedQuery = null;

            foreach (var s in sortOrders)
            {
                var action = s.Trim().ToLower();
                // 判斷是否為第一個排序條件
                bool isFirst = (orderedQuery == null);

                switch (action)
                {
                    // ✨✨✨ 新增這裡：報酬率排序邏輯 ✨✨✨
                    case "fee_rate_desc": 
                        // 邏輯：(Fee / Price) 由大到小
                        // 注意：加上 (c.Price == 0 ? 0 : ...) 是為了防止除以零錯誤
                        orderedQuery = isFirst
                            ? query.OrderByDescending(c => c.Price == 0 ? 0 : c.Fee / c.Price)
                            : orderedQuery!.ThenByDescending(c => c.Price == 0 ? 0 : c.Fee / c.Price);
                        break;

                    case "price_asc": // 價格由低到高
                        orderedQuery = isFirst ? query.OrderBy(c => c.Price) : orderedQuery!.ThenBy(c => c.Price);
                        break;

                    case "price_desc": // 價格由高到低
                        orderedQuery = isFirst ? query.OrderByDescending(c => c.Price) : orderedQuery!.ThenByDescending(c => c.Price);
                        break;

                    case "deadline_asc": // 截止日由近到遠
                        // 處理 Null 值，將 Null 視為最大值放到最後
                        orderedQuery = isFirst
                            ? query.OrderBy(c => c.Deadline ?? DateTime.MaxValue)
                            : orderedQuery!.ThenBy(c => c.Deadline ?? DateTime.MaxValue);
                        break;

                    case "deadline_desc": // 截止日由遠到近
                        orderedQuery = isFirst
                            ? query.OrderByDescending(c => c.Deadline ?? DateTime.MinValue)
                            : orderedQuery!.ThenByDescending(c => c.Deadline ?? DateTime.MinValue);
                        break;
                }
            }

            // 如果沒有指定排序，預設使用建立時間倒序 (最新的在前面)
            var finalQuery = orderedQuery ?? query.OrderByDescending(c => c.CreatedAt);

            // 6. 資料投影 (Select)
            // 只回傳前端需要的欄位，避免過度傳輸
            var results = finalQuery.Select(c => new
            {
                c.Title,
                c.Price,
                c.Quantity,
                // 如果有 Google Place 資料就顯示詳細地址，否則顯示使用者輸入的地點名稱
                Location = c.Place != null ? c.Place.FormattedAddress : c.Location,
                c.Category,
                c.ImageUrl,
                c.Deadline,
                c.Description,
                c.Currency,
                c.ServiceCode,
                // 回傳 Fee 供前端計算報酬率使用
                c.Fee
            })
            .Take(50) // 限制回傳筆數上限為 50 筆
            .ToList();

            return Ok(new { success = true, count = results.Count, data = results });
        }

        // 取得前五名熱門地點 (根據委託單數量統計)
        [HttpGet("top5-locations")]
        public IActionResult GetTopLocations()
        {
            var topLocations = _context.Commissions
                .Where(c => c.Location != null) // 排除地點為空的資料
                .GroupBy(c => c.Location)       // 依照地點名稱分組
                .Select(g => new
                {
                    Location = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count) // 依照數量由多到少排序
                .Take(5)                         // 只取前五名
                .ToList();

            return Ok(new
            {
                success = true,
                data = topLocations
            });
        }
    }
}