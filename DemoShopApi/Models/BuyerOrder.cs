using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoShopApi.Models;

public partial class BuyerOrder
{
    public int BuyerOrderId { get; set; }

    public string BuyerUid { get; set; } = null!;

    public int StoreId { get; set; }

    public decimal TotalAmount { get; set; }

    public string ReceiverName { get; set; } = null!;

    public string ReceiverPhone { get; set; } = null!;

    public string ShippingAddress { get; set; } = null!;

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
    
    public int ProductId { get; set; }

    public int Quantity { get; set; }
    
    // 在 BuyerOrder 類別裡加入
    [Column("logistics_name")]
    public string? LogisticsName { get; set; }

    [Column("tracking_number")]
    public string? TrackingNumber { get; set; }

    public virtual ICollection<BuyerOrderDetail> BuyerOrderDetails { get; set; } = new List<BuyerOrderDetail>();

    public virtual Store Store { get; set; } = null!;
    
    // 這是為了讓 EF Core 知道 ProductId 對應到哪個商品物件
    [ForeignKey("ProductId")]
    public virtual StoreProduct StoreProduct { get; set; } = null!;
}
