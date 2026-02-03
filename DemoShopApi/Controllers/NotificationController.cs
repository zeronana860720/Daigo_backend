using DemoShopApi.Data; // 記得引用你的 Context namespace
using DemoShopApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoShopApi.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    public class NotificationController : ControllerBase
    {
        private readonly DaigoContext _context;

        // 注入 DaigoContext，就像你的 CommissionController 一樣
        public NotificationController(DaigoContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            // ✨ 使用 LINQ 操作，語法優雅多了！
            var notifications = await _context.Notifications
                // 1. 先確認這個 DbSet 名字是不是叫 Notifications (如果沒有要在 Context 加喔)
                .OrderByDescending(n => n.SentAt) // 依照時間倒序
                .Take(5) // 只取前 5 筆
                .Select(n => new
                {
                    title = n.Title,
                    content = n.Content,
                    // 轉成 HH:mm 字串回傳給前端
                    time = n.SentAt.ToString("HH:mm") 
                })
                .ToListAsync();

            return Ok(notifications);
        }
    }
}