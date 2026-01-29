using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoShopApi.Models;
using DemoShopApi.DTOs;
using DemoShopApi.Data;

[ApiController]
[Route("api/buyer/orders")]
public class BuyerOrderApiController : ControllerBase
{
    private readonly StoreDbContext _db;

    public BuyerOrderApiController(StoreDbContext db)
    {
        _db = db;
    }
  
    [HttpPost] // 建立買家訂單
    public async Task<IActionResult> CreateBuyerOrder([FromBody] CreateBuyerOrderDto dto)
    {
        if (dto.Items == null || !dto.Items.Any())
            return BadRequest("訂單必須包含至少一項商品");

        var store = await _db.Stores
            .FirstOrDefaultAsync(s => s.StoreId == dto.StoreId && s.Status == 3);

        if (store == null)
            return BadRequest("賣場不存在或尚未發布");

        // 驗證商品 + 計算金額
        decimal totalAmount = 0;
        var orderItems = new List<BuyerOrderDetail>();

        foreach (var item in dto.Items)
        {
            var product = await _db.StoreProducts
                .FirstOrDefaultAsync(p =>
                    p.ProductId == item.StoreProductId &&
                    p.StoreId == dto.StoreId &&
                    p.Status == 3);

            if (product == null)
                return BadRequest($"商品不存在或不可販售");

            if (product.Quantity < item.Quantity)
                return BadRequest($"商品庫存不足");

            var subtotal = product.Price * item.Quantity;
            totalAmount += subtotal;

            orderItems.Add(new BuyerOrderDetail
            {
                StoreProductId = product.ProductId,
                ProductName = product.ProductName,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                SubtotalAmount = subtotal
            });

            // 預扣庫存（稍後 SaveChanges）
            product.Quantity -= item.Quantity;
        }

        // 建立訂單主檔
        var order = new BuyerOrder
        {
            BuyerUid = dto.BuyerUid, // 之後可改成從 JWT 取
            StoreId = dto.StoreId,
            TotalAmount = totalAmount,

            ReceiverName = dto.ReceiverName,
            ReceiverPhone = dto.ReceiverPhone,
            ShippingAddress = dto.ShippingAddress,

            Status = 0, // 已成立 但未付款
            CreatedAt = DateTime.Now
        };

        _db.BuyerOrders.Add(order);
        await _db.SaveChangesAsync(); // 先拿到 buyer_order_id

        // 建立訂單明細
        foreach (var item in orderItems)
        {
            item.BuyerOrderId = order.BuyerOrderId;
            _db.BuyerOrderDetails.Add(item);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "訂單建立成功",
            orderId = order.BuyerOrderId,
            totalAmount = order.TotalAmount
        });
    }

    [HttpGet("{orderId}/getbuyerorder")] //查詢單筆訂單（含明細）
    public async Task<IActionResult> GetBuyerOrder(int orderId)
    {
        var order = await _db.BuyerOrders
            .FirstOrDefaultAsync(o => o.BuyerOrderId == orderId);

        if (order == null)
            return NotFound("訂單不存在");

        var items = await _db.BuyerOrderDetails
            .Where(d => d.BuyerOrderId == orderId)
            .Select(d => new BuyerOrderDetailDto
            {
                ProductName = d.ProductName,
                UnitPrice = d.UnitPrice,
                Quantity = d.Quantity,
                SubtotalAmount = d.SubtotalAmount
            })
            .ToListAsync();

        var result = new BuyerOrderResponseDto
        {
            BuyerOrderId = order.BuyerOrderId,
            BuyerUid = order.BuyerUid,
            StoreId = order.StoreId,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Items = items
        };

        return Ok(result);
    }

    [HttpPut("{orderId}/updatestatus")]    // 更新訂單狀態（給錢包 / 系統用）
    public async Task<IActionResult> UpdateOrderStatus(int orderId,[FromBody] UpdateOrderStatusDto dto)
    {
        var order = await _db.BuyerOrders
            .FirstOrDefaultAsync(o => o.BuyerOrderId == orderId);

        if (order == null)
            return NotFound("訂單不存在");

        // 0: 已建立(未付款)
        // 1: 已付款
        // 2: 已取消
        // 3: 已完成

        if (dto.Status < 0 || dto.Status > 3)
            return BadRequest("不合法的訂單狀態");

        // 已取消 / 已完成 不可再變更
        if (order.Status == 2 || order.Status == 3)
            return BadRequest("訂單已結束，無法再變更狀態");

        order.Status = dto.Status;

        if (dto.Status == 2) // 取消
        {
            // 若你有 CancelledAt 可在此填
        }
        else if (dto.Status == 3) // 完成
        {
            order.CompletedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "訂單狀態已更新",
            orderId = order.BuyerOrderId,
            status = order.Status
        });
    }


}
