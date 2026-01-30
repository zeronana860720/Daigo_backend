using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;
using DemoShopApi.DTOs;
using DemoShopApi.services;


namespace DemoShopApi.Controllers
{
    [ApiController]
    [Route("api/createstore")]  // ✓ 改:移除 {storeId},避免重複
    [Tags("4 StoreProduct")]  // ✓ 改:更簡潔的名稱

    public class StoreProductController : ControllerBase  // ✓ 改:更簡潔的 Controller 名稱
    {
        private readonly StoreDbContext _db;
        private readonly ImageUploadService _imageService;
        
        public StoreProductController(StoreDbContext db, ImageUploadService imageService)
        {
            _db = db;
            _imageService = imageService;
        }
        
        [HttpPost("{storeId}/products")]  // ✓ 改:更簡潔的路由
        public async Task<IActionResult> CreateProduct(int storeId, [FromForm] CreateStoreProductDto dto)
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

            // 處理地點資料
            int? placeId = null;
            if (!string.IsNullOrEmpty(dto.GooglePlaceId))
            {
                // 先檢查這個地點是否已經存在
                var existingPlace = await _db.StoreProductPlaces
                    .FirstOrDefaultAsync(p => p.GooglePlaceId == dto.GooglePlaceId);

                if (existingPlace != null)
                {
                    // 地點已存在，直接使用
                    placeId = existingPlace.PlaceId;
                }
                else
                {
                    // 地點不存在，建立新的地點記錄
                    var newPlace = new StoreProductPlace
                    {
                        GooglePlaceId = dto.GooglePlaceId,
                        Name = dto.LocationName,
                        FormattedAddress = dto.FormattedAddress,
                        Latitude = dto.Latitude,
                        Longitude = dto.Longitude,
                        CreatedAt = DateTime.Now
                    };

                    _db.StoreProductPlaces.Add(newPlace);
                    await _db.SaveChangesAsync();

                    placeId = newPlace.PlaceId;
                }
            }

            // 存圖
            var imagePath = await _imageService.SaveProductImageAsync(dto.Image);

            // 建立商品
            var product = new StoreProduct
            {
                StoreId = storeId,
                ProductName = dto.ProductName,
                Price = dto.Price,
                Quantity = dto.Quantity,
                Description = dto.Description,
                EndDate = dto.EndDate,
                Location = dto.LocationName,
                
                PlaceId = placeId,
                ImagePath = imagePath,

                Status = 1,
                IsActive = false,
                Category = dto.Category,
                CreatedAt = DateTime.Now
            };

            _db.StoreProducts.Add(product);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品已建立，等待審核",
                productId = product.ProductId,
                imagePath = product.ImagePath,
                placeId = placeId
            });
        }

       
        [HttpPut("{storeId}/products/{productId}/price-quantity")]  // ✓ 改:更清晰的路由結構
        public async Task<IActionResult> UpdatePriceAndQuantity(int storeId, int productId, [FromBody] UpdateNewProductDto dto)
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

      
        [HttpPut("{storeId}/products/{productId}/resubmit")]  // ✓ 改:更清晰的路由
        public async Task<IActionResult> UpdateProductForReview(int storeId, int productId, [FromForm] UpdateNewProductReviewDto dto)
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

            // 存新圖
            var newImagePath = await _imageService.SaveProductImageAsync(dto.Image);

            // 更新名稱
            product.ProductName = dto.ProductName;

            if (newImagePath != null)
            {
                // 刪舊圖
                _imageService.DeleteImage(product.ImagePath);

                product.ImagePath = newImagePath;
            }

            // 重新進審核
            product.Status = 1;
            product.IsActive = false;
            product.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "商品已更新，重新進入審核"
            });
        }
 
        [HttpDelete("{storeId}/products/{productId}")]  // ✓ 改:使用標準 RESTful 路由
        public async Task<IActionResult> DeactivateProduct(int storeId, int productId)  // ✓ 改:方法名稱大寫開頭
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

        [HttpPut("products/{productId}/resubmit-rejected")]  // ✓ 改:更清晰的路由名稱
        public async Task<IActionResult> ResubmitRejectedProduct(int productId, [FromForm] ResubmitProductDto dto)
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


        [HttpPut("{storeId}/products/{productId}/activate")]  // ✓ 改:使用 activate 更符合語義
        public async Task<IActionResult> ActivateProduct(int storeId, int productId)  // ✓ 改:更清楚的方法名稱
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
