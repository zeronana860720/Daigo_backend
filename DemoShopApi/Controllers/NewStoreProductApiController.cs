using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;
using DemoShopApi.DTOs;
using DemoShopApi.services;


namespace DemoShopApi.Controllers
{
    [ApiController]
    [Route("api/store/{storeId}/products/newproducts")]
    [Tags("4 NewStoreProductApi")]

    public class NewStoreProductApiController : ControllerBase
    {
        private readonly StoreDbContext _db;
        private readonly ImageUploadService _imageService;
        public NewStoreProductApiController(StoreDbContext db, ImageUploadService imageService)
        {
            _db = db;
            _imageService = imageService;
        }
        [HttpPost] //  已發布的賣場下新增商品（第二波）
        public async Task<IActionResult> CreateNewProduct(int storeId,[FromForm] CreateStoreProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var store = await _db.Stores
                .FirstOrDefaultAsync(s => s.StoreId == storeId);

            if (store == null)
                return NotFound("賣場不存在");

            // 停權中直接擋
            if (store.Status == 4)
            {
                return BadRequest("賣場停權中，暫時無法新增商品");
            }

            // 只能在已發布的賣場新增
            if (store.Status != 3)
                return BadRequest("僅限已發布賣場可新增商品");

            // ⭐ NEW 系列：用 Service 存圖
            var imagePath = await _imageService.SaveProductImageAsync(dto.Image);

            var product = new StoreProduct
            {
                StoreId = storeId,
                ProductName = dto.ProductName,
                Price = dto.Price,
                Quantity = dto.Quantity,
                Description = dto.Description,
                EndDate = dto.EndDate,
                Location = dto.Location,

                ImagePath = imagePath,

                Status = 1,       // 新商品 -> 待審核
                IsActive = false, // 審核中不可於前端顯示
                CreatedAt = DateTime.Now
            };

            _db.StoreProducts.Add(product);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品已建立，等待審核",
                product.ProductId,
                product.ImagePath
            });
        }
       
        [HttpPut("{productId}/update-price-quantity")]// 已發布商品僅允許調整 價格跟數量（不送審） 
        public async Task<IActionResult> UpdateNewProduct(int storeId,int productId,[FromBody] UpdateNewProductDto dto)
        {
            var product = await _db.StoreProducts
            .Include(p => p.Store)
            .FirstOrDefaultAsync(p =>
             p.ProductId == productId &&
             p.StoreId == storeId);

            if (product == null)
                return NotFound("商品不存在");

            var store = product.Store;

            if (store.Status == 4)
                return BadRequest("賣場停權中，無法修改商品");

            // 僅限已發布商品
            if (product.Status != 3)
                return BadRequest("商品尚未發布，無法使用此操作");

            // 驗證
            if (dto.Price < 0 || dto.Quantity < 0)
            {
                return BadRequest("價格或數量不可小於 0");
            }

            if (dto.Price > 50000 || dto.Quantity > 500)
            {
                return BadRequest("價格不可大於50000數量不可以大於500");
            }

            product.Price = dto.Price;
            product.Quantity = dto.Quantity;
            product.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品價格 / 數量已更新"
            });
        }

      
        [HttpPut("{productId}/updatereview")]  // 修改商品名稱跟圖片則強制重新審核
        public async Task<IActionResult> UpdateNewProductReview(int storeId,int productId,[FromForm] UpdateNewProductReviewDto dto)
        {
            var product = await _db.StoreProducts
             .Include(p => p.Store)
             .FirstOrDefaultAsync(p =>
             p.ProductId == productId &&
             p.StoreId == storeId);

            if (product == null)
                return NotFound("商品不存在");

            var store = product.Store;

            if (store.Status == 4)
            {
                return BadRequest("賣場已停權，無法修改商品");
            }

            if (product.Status != 3)
            {
                return BadRequest("只有已發布商品才能使用此操作");
            }

            if (string.IsNullOrWhiteSpace(dto.ProductName))
            {
                return BadRequest("商品名稱不可為空");
            }

            // ⭐ NEW 系列：存新圖
            var newImagePath = await _imageService.SaveProductImageAsync(dto.Image);

            // 更新名稱（一定會進審核）
            product.ProductName = dto.ProductName;

            if (newImagePath != null)
            {
                // 刪舊圖
                _imageService.DeleteImage(product.ImagePath);

                product.ImagePath = newImagePath;
            }

            // 一律重新進審核
            product.Status = 1;
            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品已更新，重新進入審核"
            });
        }
 
        [HttpDelete("{productId}/deactivate")] // 下架商品（不刪資料）
        public async Task<IActionResult> deleteProduct(int storeId,int productId)
        {
            var product = await _db.StoreProducts
                .Include(p => p.Store)
                .FirstOrDefaultAsync(p =>
                    p.ProductId == productId &&
                    p.StoreId == storeId);

            if (product == null)
                return NotFound("商品不存在");

            var store = product.Store;

            if (store.Status == 4)
                return BadRequest("賣場已停權，無法操作商品");

            if (!product.IsActive)
            {
                return BadRequest("商品已是下架狀態");
            }

            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品已下架"
            });
        }

        [HttpPut("/api/products/{productId}/resubmit")] // 被退件的第二波商品修改後重新送審
        public async Task<IActionResult> ResubmitProduct(int productId,[FromForm] ResubmitProductDto dto)
        {
            var product = await _db.StoreProducts
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
                return NotFound("商品不存在");

            if (product.Status != 2)
                return BadRequest("只有審核失敗的商品才能重新送審");

            product.ProductName = dto.ProductName;
            product.Price = dto.Price;
            product.Quantity = dto.Quantity;

            if (dto.Image != null)
            {
                var imagePath = await _imageService.SaveProductImageAsync(dto.Image);
                product.ImagePath = imagePath;
            }

            product.Status = 1;
            product.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品已修改並重新送審",
                productId = product.ProductId,
                status = product.Status
            });
        }


        [HttpPut("{productId}/visible")]// 重新上架商品
        public async Task<IActionResult> VisibleProduct(int storeId,int productId)
        {
            var product = await _db.StoreProducts
                .Include(p => p.Store)
                .FirstOrDefaultAsync(p =>
                    p.ProductId == productId &&
                    p.StoreId == storeId);

            if (product == null)
                return NotFound("商品不存在");

            var store = product.Store;

            if (store.Status == 4)
                return BadRequest("賣場已停權，無法上架商品");

            // 只能上架已發布商品
            if (product.Status != 3)
                return BadRequest("商品尚未通過審核，無法上架");

            if (product.IsActive)
                return BadRequest("商品已是上架狀態");

            product.IsActive = true;
            product.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品已重新上架"
            });
        }

    }
}
