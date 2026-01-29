using System;
using System.Collections.Generic;

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

    public virtual ICollection<BuyerOrderDetail> BuyerOrderDetails { get; set; } = new List<BuyerOrderDetail>();

    public virtual Store Store { get; set; } = null!;
}
