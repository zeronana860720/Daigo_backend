using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;
using DemoShopApi.DTOs;
using Microsoft.AspNetCore.Authorization;

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
    private string GetCurrentSellerUid()
    {
        var sellerUid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sellerUid))
        {
            // 這裡你也可以選擇 return null，看你想怎麼處理
            throw new UnauthorizedAccessException("找不到使用者 Uid，請確認已登入並帶入 JWT。");
        }

        return sellerUid;
    }
    
    [Authorize]// done
    [HttpPost("my/store")]
    public async Task<IActionResult> CreateStore([FromForm] CreateStoreDto dto)
    {
        // 1. 取得目前登入者的 Uid (賣家身分確認)
        var sellerUid = GetCurrentSellerUid();

        // 2. 數量檢查邏輯：確保賣家沒有超過 10 個賣場
        int storeCount = await _db.Stores.CountAsync(s => s.SellerUid == sellerUid);
        if (storeCount >= 10)
        {
            return BadRequest(new { message = "此賣家最多只能建立 10 個賣場 (｡>﹏<｡)" });
        }

        // 3. 圖片處理邏輯：把圖片從包裹 (Dto) 拿出來存到電腦裡
        string? savedPath = null;
        if (dto.StoreImage != null && dto.StoreImage.Length > 0)
        {
            // 設定存檔路徑：存到 wwwroot/uploads
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // 幫圖片取個獨一無二的名字
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.StoreImage.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            // 執行存檔動作
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.StoreImage.CopyToAsync(stream);
            }
        
            // 這是要存進資料庫的「圖片路徑字串」
            savedPath = $"/uploads/{fileName}";
        }

        // 4. 資料庫寫入邏輯：建立新的賣場物件
        var store = new Store
        {
            SellerUid = sellerUid,
            StoreName = dto.StoreName,
            StoreImage = savedPath,    // 這裡存的是剛才產生的路徑喔！
            Status = 0,               // 預設為草稿狀態
            StoreDescription = dto.StoreDescription,
            ReviewFailCount = 0,
            CreatedAt = DateTime.Now
        };

        _db.Stores.Add(store);
        await _db.SaveChangesAsync();

        // 5. 回傳結果
        return Ok(new
        {
            store.StoreId,
            store.StoreName,
            store.StoreImage,
            store.StoreDescription
        });
    }


    // done
    [HttpGet("my/mystore")] // 改路由，不用傳 sellerUid
    [Authorize]
    public async Task<IActionResult> GetMyStore()
    {
        var sellerUid = GetCurrentSellerUid(); // 從 token 抓

        var stores = await _db.Stores
            .Where(s => s.SellerUid == sellerUid)
            .Select(s => new // 改成 anonymous object，包含圖片
            {
                s.StoreId,
                s.StoreName,
                s.Status,
                s.StoreImage,      // ⬅ 加這行
                s.CreatedAt,
                s.ReviewFailCount,
                s.StoreDescription
                
            })
            .OrderByDescending(s => s.CreatedAt)
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
              .Where(p => p.Status == 3)
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
   
    // done 
    [HttpPost("{storeId}/submit")] //  賣家送審賣場
    public async Task<IActionResult> SubmitStore(int storeId)
    {
        // 1️從資料庫找出這個賣場
        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreId == storeId);

        // 2️檢查賣場是否存在
        if (store == null)
            return NotFound("賣場不存在");
    
        // 3️檢查賣場是否被停權
        if (store.Status == 4)
            return BadRequest("賣場已停權，無法送審");

        // 4️檢查賣場狀態是否允許送審 (只允許草稿0 或 失敗2 的狀態送審)
        if (store.Status != 0 && store.Status != 2)
            return BadRequest("目前賣場狀態不可送審");

        // 5️把賣場狀態改成「審核中」
        store.Status = 1;
    
        // 6️記錄送審時間
        store.SubmittedAt = DateTime.Now;

        // 7️儲存到資料庫
        await _db.SaveChangesAsync();

        // 8️回傳成功訊息
        return Ok(new
        {
            message = "賣場已送審"
        });
    }


}
