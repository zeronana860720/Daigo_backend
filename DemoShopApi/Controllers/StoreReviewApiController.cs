using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;
using DemoShopApi.DTOs;


[ApiController]
[Route("api/review")]
[Tags("3 StoreReviewApi")]

public class StoreReviewApiController : ControllerBase
{
    private readonly StoreDbContext _db;

    public StoreReviewApiController(StoreDbContext db)
    {
        _db = db;
    }
    
    [HttpGet("storepending")] // 第一波 撈賣場+第一波賞品資料
    public async Task<IActionResult> GetPendingStores()
    {
        var stores = await _db.Stores
            .Include(s => s.StoreProducts)
            .Where(s => s.Status == 1)
            .ToListAsync();

        var result = stores.Select(s => new StoreReviewListDto
        {
            StoreId = s.StoreId,
            SellerId = s.SellerUid,
            StoreName = s.StoreName,
            Status = s.Status,
            ReviewFailCount = s.ReviewFailCount,
            CreatedAt = s.CreatedAt,

            StoreProducts = s.StoreProducts
            .Where(p => p.Status == 1) // 只顯示送審中的商品
            .Select(p => new StoreReviewProductDto
            {
            ProductId = p.ProductId,
            ProductName = p.ProductName,
            Price = p.Price,
            Quantity = p.Quantity
            })
            .ToList()

        }).ToList();

        return Ok(result);
    }
  
    [HttpPost("{storeId}/storeapprove")]  // 賣場審核通過
    public async Task<IActionResult> ApproveStore(int storeId, [FromBody] ReviewDto dto)
    {
        var store = await _db.Stores
             .Include(s => s.StoreProducts) //  一定要 Include 商品
             .FirstOrDefaultAsync(s => s.StoreId == storeId);

        if (store == null)
            return NotFound();

        // 賣場通過
        store.Status = 3;             // 已發布
        store.ReviewFailCount = 0;

        // 連同賣場一起啟用第一波商品
        foreach (var product in store.StoreProducts)
        {
            product.Status = 3;       // 已發布
            product.IsActive = true;  // 前端顯示
        }

        // 寫入審核紀錄
        // _db.StoreReviews.Add(new StoreReview
        // {
        //     // ProductId = productId,     // 記錄商品ID
        //     ReviewerUid = dto.ReviewerUid,
        //     Result = 1,                // 1 = 通過 (假設 enum/convention)
        //     CreatedAt = DateTime.Now
        // });

        await _db.SaveChangesAsync();
        return Ok(new { message = "賣場審核通過，第一波商品已同步上架" });
    }

    
    [HttpPost("{storeId}/rejectstore")]// 賣場審核不通過
    public async Task<IActionResult> RejectStore(int storeId, [FromBody] ReviewDto dto)
    {
        var store = await _db.Stores
      .Include(s => s.StoreProducts)
      .FirstOrDefaultAsync(s => s.StoreId == storeId);

        if (store == null) 
        return NotFound("賣場不存在");

        if (store.Status != 1)
            return BadRequest("賣場非審核中狀態，無法審核");

        // 累加失敗次數
        store.ReviewFailCount += 1;

        if (store.ReviewFailCount >= 5)
        {
            // 超過次數 → 停權
            store.Status = 4;
            store.RecoverAt = DateTime.Now.AddDays(7);

            // 停權時，商品才一起進 4
            foreach (var product in store.StoreProducts)
            {
                product.Status = 4;
                product.IsActive = false;
            }
        }
        else
        {
            // 尚未達上限 → 審核失敗
            store.Status = 2;
            store.RecoverAt = null;
        }

        // _db.StoreReviews.Add(new StoreReview
        // {
        //     // ProductId = productId,
        //     ReviewerUid = dto.ReviewerUid,
        //     Result = 2,                   // 2 = 不通過
        //     Comment = dto.Comment,
        //     CreatedAt = DateTime.Now
        // });

        await _db.SaveChangesAsync();
        return Ok(new
        {
            message = store.Status == 4
              ? "賣場審核未通過，已達上限並遭停權"
              : $"賣場審核未通過（目前失敗次數：{store.ReviewFailCount}/5）"
        });
    }
}
