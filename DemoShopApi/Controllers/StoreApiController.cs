using System.Security.Claims;
using DemoShopApi.Data;
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
    
    private readonly DaigoContext _daigoDb;

    public DemoShopApiController(StoreDbContext db , DaigoContext daigoDb)
    {
        _db = db;
        _daigoDb = daigoDb;
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

    [HttpGet("{storeId}/products")] // ✓ 改:更符合 RESTful 的路由
    public async Task<IActionResult> GetStoreProducts(int storeId) // ✓ 改:更明確的方法名稱
    {
        // 1️先確認賣場是否存在
        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreId == storeId);

        if (store == null)
            return NotFound("賣場不存在");

        // 2️取得該賣場下的所有商品
        var products = await _db.StoreProducts
            .Where(p => p.StoreId == storeId)
            .OrderByDescending(p => p.CreatedAt) // 最新的商品排在前面
            .Select(p => new
            {
                // 商品基本資料
                p.ProductId,
                p.ProductName,
                p.Description,
                p.Price,
                p.Quantity,
                p.Category,
                p.Location,
            
                // 商品狀態
                p.Status,        // 0:草稿, 1:審核中, 2:審核失敗, 3:已發布
                p.IsActive,      // true:上架, false:下架
                p.RejectReason,  // 審核失敗原因
            
                // 圖片與時間
                p.ImagePath,
                p.EndDate,
                p.CreatedAt,
                p.UpdatedAt,
            
                // 新增:地點資訊 (如果有關聯的話)
                // 透過導覽屬性跳到另一張表
                Place = p.Place == null ? null : new
                {
                    p.Place.PlaceId,
                    p.Place.Name,
                    p.Place.FormattedAddress,
                    p.Place.Latitude,
                    p.Place.Longitude,
                    p.Place.GooglePlaceId
                }
            })
            .ToListAsync();

        // 3️⃣ 回傳商品列表
        return Ok(new
        {
            storeId = storeId,
            storeName = store.StoreName,
            storeStatus = store.Status,
            totalProducts = products.Count,
            products = products
        });
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

    // 編輯賣場
    [Authorize]
    [HttpPut("{storeId}/update")]  // PUT 方法用於更新
    public async Task<IActionResult> UpdateStore(int storeId, [FromForm] UpdateStoreDto dto)
    {
        // 1. 取得目前登入者的 Uid
        var sellerUid = GetCurrentSellerUid();

        // 2. 找出要編輯的賣場
        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.SellerUid == sellerUid);

        // 3. 檢查賣場是否存在 & 是否為本人的賣場
        if (store == null)
            return NotFound(new { message = "賣場不存在或無權限編輯" });

        // 4. 更新賣場名稱
        if (!string.IsNullOrWhiteSpace(dto.StoreName))
        {
            store.StoreName = dto.StoreName;
        }

        // 5. 更新賣場描述
        store.StoreDescription = dto.StoreDescription;

        // 6. 處理圖片上傳 (如果有新圖片)
        if (dto.StoreImage != null && dto.StoreImage.Length > 0)
        {
            // 刪除舊圖片 (如果存在)
            if (!string.IsNullOrEmpty(store.StoreImage))
            {
                var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", store.StoreImage.TrimStart('/'));
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            // 上傳新圖片
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(dto.StoreImage.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.StoreImage.CopyToAsync(stream);
            }

            store.StoreImage = $"/uploads/{fileName}";
        }

        // 7. 編輯後狀態變成「審核中」
        store.Status = 1;
        store.SubmittedAt = DateTime.Now;
        store.UpdatedAt = DateTime.Now;

        // 8. 儲存到資料庫
        await _db.SaveChangesAsync();

        // 9. 回傳結果
        return Ok(new
        {
            message = "賣場更新成功，已送交審核",
            store.StoreId,
            store.StoreName,
            store.StoreImage,
            store.StoreDescription,
            store.Status
        });
    }
    
    // 關閉賣場
    [Authorize]
    [HttpPost("{storeId}/close")]  // 賣家關閉自己的賣場
    public async Task<IActionResult> CloseStore(int storeId)
    {
        // 1. 取得目前登入者的 Uid
        var sellerUid = GetCurrentSellerUid();

        // 2. 找出要關閉的賣場 (確認是本人的)
        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.SellerUid == sellerUid);

        // 3. 檢查賣場是否存在
        if (store == null)
            return NotFound(new { message = "賣場不存在或無權限操作" });

        // 4. 檢查賣場是否已經是關閉狀態
        if (store.Status == 5)
            return BadRequest(new { message = "賣場已經是關閉狀態" });

        // 5. 把賣場狀態改成「已關閉」
        store.Status = 5;
        store.UpdatedAt = DateTime.Now;

        // 6. 商品狀態不變！只改賣場狀態
        // (刪除原本修改商品的程式碼)

        // 7. 儲存到資料庫
        await _db.SaveChangesAsync();

        // 8. 回傳成功訊息
        return Ok(new
        {
            message = "賣場已關閉",
            storeId = store.StoreId,
            storeName = store.StoreName,
            status = store.Status
        });
    }

    
    // 啟用賣場
    [Authorize]
    [HttpPost("{storeId}/reopen")]  // 賣家重新啟用賣場
    public async Task<IActionResult> ReopenStore(int storeId)
    {
        // 1. 取得目前登入者的 Uid
        var sellerUid = GetCurrentSellerUid();

        // 2. 找出要啟用的賣場 (確認是本人的)
        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.SellerUid == sellerUid);

        // 3. 檢查賣場是否存在
        if (store == null)
            return NotFound(new { message = "賣場不存在或無權限操作" });

        // 4. 檢查賣場是否是關閉狀態
        if (store.Status != 5)
            return BadRequest(new { message = "賣場不是關閉狀態" });

        // 5. 把賣場狀態改回「營業中」
        store.Status = 1;
        store.UpdatedAt = DateTime.Now;

        // 6. 商品狀態不變！只改賣場狀態
        // (刪除原本修改商品的程式碼)

        // 7. 儲存到資料庫
        await _db.SaveChangesAsync();

        // 8. 回傳成功訊息
        return Ok(new
        {
            message = "賣場已重新啟用",
            storeId = store.StoreId,
            storeName = store.StoreName,
            status = store.Status
        });
    }
    
    // 獲取商品列表
    [HttpGet("allproducts")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _db.StoreProducts
            .Include(p => p.Place)  // Join StoreProduct_Place 表
            .Where(p => p.Status == 3)  // 只拿上架中的商品
            .Select(p => new
            {
                id = p.ProductId,
                name = p.ProductName,
                price = p.Price,
                image = p.ImagePath,
                category = p.Category,
                location = p.Location,
                deadline = p.EndDate,
                status = "販售中",
            
                // 地點詳細資訊 (如果有的話)
                placeDetails = p.Place == null ? null : new
                {
                    placeId = p.Place.PlaceId,
                    googlePlaceId = p.Place.GooglePlaceId,
                    name = p.Place.Name,
                    address = p.Place.FormattedAddress,
                    latitude = p.Place.Latitude,
                    longitude = p.Place.Longitude,
                    mapUrl = p.Place.MapUrl
                }
            })
            .ToListAsync();

        return Ok(products);
    }
    
    
    [HttpGet("products/{productId}")]
    public async Task<IActionResult> GetProductDetail(int productId)
    {
        var product = await _db.StoreProducts
            .Include(p => p.Place)      // Join 地點表
            .Include(p => p.Store)      // Join 賣場表
            .Where(p => p.ProductId == productId && p.Status == 3)  // 只拿上架中的
            .Select(p => new
            {
                // 商品資訊
                id = p.ProductId,
                name = p.ProductName,
                price = p.Price,
                quantity = p.Quantity,
                image = p.ImagePath,
                category = p.Category,
                location = p.Location,
                deadline = p.EndDate,
                description = p.Description,
                status = "販售中",
                storeId = p.Store.StoreId,
            
                // 賣場資訊
                storeInfo = new
                {
                    storeId = p.Store.StoreId,
                    name = p.Store.StoreName,
                    image = p.Store.StoreImage,
                    description = p.Store.StoreDescription
                },
            
                // 地點詳細資訊
                placeDetails = p.Place == null ? null : new
                {
                    placeId = p.Place.PlaceId,
                    googlePlaceId = p.Place.GooglePlaceId,
                    name = p.Place.Name,
                    address = p.Place.FormattedAddress,
                    latitude = p.Place.Latitude,
                    longitude = p.Place.Longitude,
                    mapUrl = p.Place.MapUrl
                }
            })
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound("商品不存在或已下架");

        return Ok(product);
    }
    
    // 建立訂單
    [Authorize]
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        var buyerUid = GetCurrentSellerUid();

        using var transaction1 = await _db.Database.BeginTransactionAsync();
        
        try {
            // ✨ 加這個:檢查 StoreId 是否存在
            var storeExists = await _db.Stores
                .AnyAsync(s => s.StoreId == dto.StoreId);
            
            if (!storeExists)
            {
                return BadRequest(new { message = "賣場不存在" });
            }
            
            var user = await _daigoDb.Users
                .FirstOrDefaultAsync(u => u.Uid == buyerUid);
            
            if (user == null)
            {
                return NotFound(new { message = "找不到使用者資料" });
            }
            
            if (user.Balance < dto.TotalAmount)
            {
                return BadRequest(new { 
                    message = "餘額不足,無法完成訂單",
                    currentBalance = user.Balance,
                    required = dto.TotalAmount
                });
            }
            
            user.Balance -= dto.TotalAmount;
            
            var order = new BuyerOrder
            {
                BuyerUid = buyerUid,
                StoreId = dto.StoreId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                TotalAmount = dto.TotalAmount,
                ReceiverName = dto.ReceiverName,
                ReceiverPhone = dto.ReceiverPhone,
                ShippingAddress = dto.ShippingAddress,
                Status = 0,
                CreatedAt = DateTime.Now,
                
            };
            
            _db.BuyerOrders.Add(order);
            
            await _daigoDb.SaveChangesAsync();
            await _db.SaveChangesAsync();
            
            await transaction1.CommitAsync();
            
            return Ok(new { 
                message = "訂單建立成功!",
                remainingBalance = user.Balance
            });
        }
        catch (Exception ex)
        {
            await transaction1.RollbackAsync();
            
            // ✨ 加這個:顯示詳細錯誤訊息
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            
            return StatusCode(500, new { 
                message = "訂單建立失敗", 
                error = innerMessage  // ✨ 改這行
            });
        }
    }

    // ✨ 1. 取得賣家所有訂單 (包含商品資訊)
    [Authorize]
    [HttpGet("my/orders")]
    public async Task<IActionResult> GetSellerOrders()
    {
        var sellerUid = GetCurrentSellerUid();

        // 邏輯：
        // 1. 找出這個賣家擁有的所有賣場 ID
        // 2. 找出這些賣場底下的所有訂單
        // 3. 同時關聯 StoreProduct (拿圖片/名稱) 和 Store (確認權限)

        var orders = await _db.BuyerOrders
            .Include(o => o.Store)           // 關聯賣場
            .Include(o => o.StoreProduct)    // 關聯商品 (這一步很重要，你的 BuyerOrder 應該要有這個導覽屬性)
            .Where(o => o.Store.SellerUid == sellerUid) // 只找這個賣家的
            .OrderByDescending(o => o.CreatedAt)        // 最新的訂單在上面
            .Select(o => new
            {
                // 訂單基本資訊
                buyerOrderId = o.BuyerOrderId,
                createdAt = o.CreatedAt,
                status = o.Status,         // 0:未出貨, 1:已出貨, 2:已完成
                totalAmount = o.TotalAmount,
                quantity = o.Quantity,

                // 買家資訊
                receiverName = o.ReceiverName,
                shippingAddress = o.ShippingAddress,

                // 商品資訊 (從關聯的 Product 拿)
                productName = o.StoreProduct != null ? o.StoreProduct.ProductName : "商品已下架或移除",
                productImage = o.StoreProduct != null ? o.StoreProduct.ImagePath : null,
                
                // 物流資訊 (如果有)
                logisticsName = o.LogisticsName,
                trackingNumber = o.TrackingNumber,
                shippedAt = o.ShippedAt
            })
            .ToListAsync();

        return Ok(orders);
    }

    // ✨ 2. 賣家執行出貨
    [Authorize]
    [HttpPost("orders/{orderId}/ship")]
    public async Task<IActionResult> ShipOrder(int orderId, [FromBody] ShipOrderDto dto)
    {
        var sellerUid = GetCurrentSellerUid();

        // 1. 找出這筆訂單 (並檢查是不是這個賣家的)
        var order = await _db.BuyerOrders
            .Include(o => o.Store)
            .FirstOrDefaultAsync(o => o.BuyerOrderId == orderId);

        if (order == null)
        {
            return NotFound(new { message = "找不到此訂單" });
        }

        // 2. 安全檢查：確認這筆訂單的賣場主人，是目前登入的人
        if (order.Store.SellerUid != sellerUid)
        {
            return Unauthorized(new { message = "你無權操作此訂單" });
        }

        // 3. 檢查狀態 (只有狀態 0 未出貨 才能出貨)
        if (order.Status != 0)
        {
            return BadRequest(new { message = "此訂單狀態無法執行出貨 (可能已出貨或取消)" });
        }

        // 4. 更新訂單資訊
        order.LogisticsName = dto.LogisticsName;
        order.TrackingNumber = dto.TrackingNumber;
        order.Status = 1;           // 設定為已出貨
        order.ShippedAt = DateTime.Now; // 紀錄時間

        // 5. 存檔
        await _db.SaveChangesAsync();

        return Ok(new { message = "出貨成功！", shippedAt = order.ShippedAt });
    }
    
    // ✨ 1. 買家取得自己的購買清單
    [Authorize]
    [HttpGet("my/purchases")]
    public async Task<IActionResult> GetMyPurchases()
    {
        var buyerUid = GetCurrentSellerUid(); // 這裡取得的是「買家」的 UID

        var orders = await _db.BuyerOrders
            .Include(o => o.Store)           // 關聯賣場 (為了顯示是在哪家店買的)
            .Include(o => o.StoreProduct)    // 關聯商品 (顯示圖片名稱)
            .Where(o => o.BuyerUid == buyerUid) // ✨ 關鍵：只找「我買的」
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                buyerOrderId = o.BuyerOrderId,
                storeName = o.Store.StoreName,  // 前端要顯示賣場名稱
                productName = o.StoreProduct != null ? o.StoreProduct.ProductName : "商品已下架",
                productImage = o.StoreProduct != null ? o.StoreProduct.ImagePath : null,
                
                status = o.Status,
                totalAmount = o.TotalAmount,
                createdAt = o.CreatedAt,
                
                // 物流資訊 (買家很關心這個)
                logisticsName = o.LogisticsName,
                trackingNumber = o.TrackingNumber
            })
            .ToListAsync();

        return Ok(orders);
    }

    // ✨ 2. 買家完成訂單 (確認收貨 + 撥款)
    [Authorize]
    [HttpPost("orders/{orderId}/complete")]
    public async Task<IActionResult> CompleteOrder(int orderId)
    {
        var buyerUid = GetCurrentSellerUid(); // 取得買家 ID

        // 1. 找出訂單，並 Include Store 以便知道賣家是誰
        var order = await _db.BuyerOrders
            .Include(o => o.Store)           // ✨ 關鍵：要關聯賣場，才拿得到 SellerUid
            .Include(o => o.StoreProduct)    // 為了紀錄 log，順便抓商品名
            .FirstOrDefaultAsync(o => o.BuyerOrderId == orderId);

        if (order == null)
        {
            return NotFound(new { message = "找不到此訂單" });
        }

        // 2. 安全檢查
        if (order.BuyerUid != buyerUid)
        {
            return Unauthorized(new { message = "你無權操作此訂單" });
        }

        // 3. 狀態檢查
        if (order.Status != 1)
        {
            return BadRequest(new { message = "只有「運送中」的訂單才能進行確認收貨喔！" });
        }

        // ---------------------------------------------------
        // ✨✨✨ 這裡開始是撥款邏輯 ✨✨✨
        // ---------------------------------------------------

        // 4. 透過 Store 找到賣家的 UID
        var sellerUid = order.Store.SellerUid;

        // 5. 去 _daigoDb (User資料庫) 撈出賣家
        var seller = await _daigoDb.Users
            .FirstOrDefaultAsync(u => u.Uid == sellerUid);

        if (seller != null)
        {
            // 6. 增加賣家餘額
            seller.Balance += order.TotalAmount;

            // 7. 寫入錢包紀錄 (WalletLog) - 這樣才有據可查
            var log = new WalletLog
            {
                Uid = sellerUid,
                Action = "Income", // 動作類型
                Amount = order.TotalAmount,
                Balance = seller.Balance??0, // 變更後的餘額
                EscrowBalance = seller.EscrowBalance??0,
                Description = $"賣場訂單收入: {order.StoreProduct?.ProductName ?? "商品"} (訂單#{order.BuyerOrderId})",
                // ServiceCode 可以放訂單編號或留空，看你的設計
                ServiceCode = $"ORDER-{order.BuyerOrderId}" 
            };

            _daigoDb.WalletLogs.Add(log);
        }
        else
        {
            // 這種情況理論上不該發生，除非賣家帳號被刪除了
            return StatusCode(500, new { message = "找不到賣家帳戶，無法撥款" });
        }

        // ---------------------------------------------------
        
        // 8. 更新訂單狀態
        order.Status = 2; // 已完成
        order.CompletedAt = DateTime.Now;

        // 9. 兩個資料庫都要存檔！
        await _daigoDb.SaveChangesAsync(); // 存錢
        await _db.SaveChangesAsync();      // 存訂單狀態

        return Ok(new { message = "訂單完成！款項已撥入賣家帳戶 🎉" });
    }

    
    
}
