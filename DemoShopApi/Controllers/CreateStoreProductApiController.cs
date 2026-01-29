using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;
using DemoShopApi.DTOs;
using DemoShopApi.services;


namespace DemoShopApi.Controllers;

[ApiController]
[Route("api/store/{storeId}/createproducts")]
[Tags("2 CreateStoreProductApi")]

public class CreateStoreProductApiController : ControllerBase
{
    private readonly StoreDbContext _db;
    private readonly ImageUploadService _imageService;



    public CreateStoreProductApiController(StoreDbContext db, ImageUploadService imageService)
    {
        _db = db;
        _imageService = imageService;
    }
  
    [HttpPost] //  建立第一波商品（商品隨賣場一起進審核）
    public async Task<IActionResult> CreateProduct(int storeId,[FromForm] CreateStoreProductDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreId == storeId);

        if (store == null)
            return NotFound("賣場不存在");

        if (store.Status == 4)
        {
            return BadRequest("賣場停權中，暫時無法操作商品");
        }

        // 只有草稿可新增商品
        if (store.Status != 0)
        {
            return BadRequest(new
            {
                message = "賣場已送審或已發布，禁止再新增商品"
            });
        }

        var imagePath = await _imageService.SaveProductImageAsync(dto.Image);

        var product = new StoreProduct
        {
            StoreId = storeId,
            ProductName = dto.ProductName,
            Description = dto.Description,
            Price = dto.Price,
            Quantity = dto.Quantity,
            Location = dto.Location,
            EndDate = dto.EndDate,

            // ⭐ 圖片重點
            ImagePath = imagePath,

            Status = 0,
            CreatedAt = DateTime.Now
        };

        _db.StoreProducts.Add(product);
        await _db.SaveChangesAsync();
        return Ok(new
        {
            product.ProductId,
            product.ImagePath,
            Message = "商品已建立，隨賣場進入審核"
        });
    }

    [HttpPut("{productId}/edit")]   // 修改審核中的商品資訊
    public async Task<IActionResult> EditProduct(int storeId,int productId,[FromForm] EditProductDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var store = await _db.Stores.FindAsync(storeId);
        if (store == null)
            return NotFound("賣場不存在");

        if (store.Status == 4)
            return BadRequest("賣場停權中，無法修改商品");

        var product = await _db.StoreProducts
            .FirstOrDefaultAsync(p => p.ProductId == productId
                                   && p.StoreId == storeId);

        if (product == null)
            return NotFound("商品不存在");

        if (product.Status == 3)
            return BadRequest("商品已發布，請使用商品修改 API");

        product.ProductName = dto.ProductName;
        product.Price = dto.Price;
        product.Quantity = dto.Quantity;
        product.Description = dto.Description;
        product.EndDate = dto.EndDate;
        product.Location = dto.Location;

        if (dto.Image != null)
        {
            var newImagePath = await _imageService.SaveProductImageAsync(dto.Image);

            if (!string.IsNullOrEmpty(newImagePath))
            {
                _imageService.DeleteImage(product.ImagePath);
                product.ImagePath = newImagePath;
            }
        }
            product.Status = 1; // 待審核
            product.IsActive = false; // 資料庫顯示0 前端不會看到
            product.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            product.ProductId,
            product.Status,
            message = "商品已更新，重新進入審核流程"
        });
    }

 
    [HttpDelete("{productId}/withdraw")]   //  移除待審核中的商品，可繼續新增商品並重新送審
    public async Task<IActionResult> Withdraw(
        int storeId,
        int productId)
    {
        var store = await _db.Stores.FindAsync(storeId);
        if (store == null)
            return NotFound("賣場不存在");

        if (store.Status == 4)
            return BadRequest("賣場已停權，無法操作商品");

        var product = await _db.StoreProducts
            .FirstOrDefaultAsync(p => p.ProductId == productId
                                   && p.StoreId == storeId);

        if (product == null)
            return NotFound("商品不存在");

        // 僅限草稿 / 審核中
        if (product.Status != 0 && product.Status != 1)
            return BadRequest("此商品狀態不可撤回");


        // 刪除僅限草稿跟審核中
        product.Status = 5; // 撤回狀態
        product.IsActive = false;
        product.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

    
        return Ok(new
        {
            message = "商品已移除，可繼續新增商品並重新送審"
        });
    }
}
