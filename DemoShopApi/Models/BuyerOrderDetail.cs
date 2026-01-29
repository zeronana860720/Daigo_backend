using System;
using System.Collections.Generic;
namespace DemoShopApi.Models;

public partial class BuyerOrderDetail
{
    public int BuyerOrderDetailId { get; set; }

    public int BuyerOrderId { get; set; }

    public int StoreProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal SubtotalAmount { get; set; }

    public virtual BuyerOrder BuyerOrder { get; set; } = null!;

    public virtual StoreProduct StoreProduct { get; set; } = null!;
}
