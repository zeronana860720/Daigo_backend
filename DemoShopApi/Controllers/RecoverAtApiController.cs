using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;

namespace DemoShopApi.Controllers
{
    [ApiController]
    [Route("api/admin/store")]
    [Tags("6 RecoverStore")]
    public class RecoverAtApiController : ControllerBase
    {
        private readonly StoreDbContext _db;

        public RecoverAtApiController(StoreDbContext db)
        {
            _db = db;
        }
        
        [HttpPost("{storeId}/recover")]  //  賣場恢復（解除停權）
        public async Task<IActionResult> RecoverStore(int storeId)
        {
            var store = await _db.Stores
                .FirstOrDefaultAsync(s => s.StoreId == storeId);

            if (store == null)
                return NotFound("賣場不存在");

            // 僅限停權狀態
            if (store.Status != 4)
                return BadRequest("賣場目前不是停權狀態");

            // 尚未到恢復時間
            if (store.RecoverAt == null || store.RecoverAt > DateTime.Now)
                return BadRequest("尚未到達恢復時間");

            // 恢復為「審核失敗」，需賣家重新送審
            store.Status = 2;
            store.ReviewFailCount = 0;
            store.RecoverAt = null;
            store.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "賣場已解除停權，請重新送審",
                storeId = store.StoreId,
                status = store.Status,
                ReviewFailCount = store.ReviewFailCount
            });
        }
    }
}
