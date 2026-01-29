using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class Store
{
    public int StoreId { get; set; }

    public string SellerUid { get; set; } = null!;

    public string StoreName { get; set; } = null!;

    //測試
    public byte Status { get; set; }

    public int ReviewFailCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? RecoverAt { get; set; }
    
    public string? StoreImage { get; set; }

    public virtual ICollection<BuyerOrder> BuyerOrders { get; set; } = new List<BuyerOrder>();

    public virtual ICollection<StoreProduct> StoreProducts { get; set; } = new List<StoreProduct>();

    public virtual ICollection<StoreReview> StoreReviews { get; set; } = new List<StoreReview>();
}
