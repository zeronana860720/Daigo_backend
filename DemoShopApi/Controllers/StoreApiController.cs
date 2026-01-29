using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;
using DemoShopApi.DTOs;

[ApiController]
[Route("api/createstore")]
[Tags("1 DemoShopApi")]

public class DemoShopApiController : ControllerBase
{
    private readonly StoreDbContext _db;

    public DemoShopApiController(StoreDbContext db)
    {
        _db = db;
    }

    [HttpPost] //  建立賣場
    public async Task<IActionResult> CreateStore([FromBody] CreateStoreDto dto)
    {
        var store = new Store
        {
            SellerUid = dto.SellerUid,
            StoreName = dto.StoreName,
            Status = 0,               // 草稿
            ReviewFailCount = 0,
            CreatedAt = DateTime.Now
        };
        // 計算此賣家已建立的賣場數量
        int storeCount = await _db.Stores
            .CountAsync(s => s.SellerUid == dto.SellerUid);

        // 若已達上限（10 個）則拒絕
        if (storeCount >= 10)
        {
            return BadRequest(new
            {
                message = "此賣家最多只能建立  10 個賣場"
            });
        }

        _db.Stores.Add(store);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            store.StoreId,
        });
    }

    
    [HttpGet("my/{sellerUid}/mystore")]//  賣家查看自己的賣場
    public async Task<IActionResult> GetMyStore(string sellerUid)
    {
        var stores = await _db.Stores
            .Where(s => s.SellerUid == sellerUid)
            .ToListAsync();

        return Ok(stores);
    }

    [HttpGet("forpublic")]   // 非會員對象可以查看賣場底下與商品
    public async Task<IActionResult> GetPublicStores()
    {
        var stores = await _db.Stores
      .Where(s => s.Status == 3) // 已發布賣場
      .Select(s => new
      {
          s.StoreId,
          s.StoreName,

          Products = s.StoreProducts
              .Where(p => p.Status == 3 && p.IsActive)
              .Select(p => new
              {
                  p.ProductId,
                  p.ProductName,
                  p.Price
              })
              .ToList()
      })
      .ToListAsync();

        return Ok(stores);
    }

    [HttpGet("{storeId}/myproduct")] // 取得賣場詳細資料（包含底下所有商品）
    public async Task<IActionResult> GetStoreDetail(int storeId)
    {
        var store = await _db.Stores
            .Where(s => s.StoreId == storeId)
            .Select(s => new
            {
                s.StoreId,
                s.StoreName,
                s.Status,
                s.ReviewFailCount,
                s.CreatedAt,

                Products = s.StoreProducts
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        p.ProductId,
                        p.ProductName,
                        p.Price,
                        p.Quantity,
                        p.Status,
                        p.IsActive,
                        p.CreatedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (store == null)
            return NotFound("賣場不存在");

        return Ok(store);
    }
   
    [HttpPost("{storeId}/submit")] //  賣家送審賣場
    public async Task<IActionResult> SubmitStore(int storeId)
    {
        var store = await _db.Stores
        .Include(s => s.StoreProducts)
        .FirstOrDefaultAsync(s => s.StoreId == storeId);

        if (store == null)
            return NotFound("賣場不存在");
        // 停權不可送
        if (store.Status == 4)
            return BadRequest("賣場已停權，無法送審");

        // 允許首次 (0) 與重新送審 (2)
        if (store.Status != 0 && store.Status != 2)
            return BadRequest("目前賣場狀態不可送審");

        if (!store.StoreProducts.Any())
            return BadRequest("賣場至少需建立一個商品才能送審");

        store.Status = 1; //  審核中
        store.SubmittedAt = DateTime.Now;

        foreach (var product in store.StoreProducts)
        {
            // 停權與撤回商品，不參與送審
            if (product.Status == 4 || product.Status == 5)
                continue;

            if (product.Status == 0 || product.Status == 2) // 尚未送審的第一波商品跟修改後的商品
            {
                product.Status = 1;      // 商品審核中
                product.IsActive = false; // 審核中前端不顯示
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "賣場已送審"
        });
    }

}
