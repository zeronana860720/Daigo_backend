using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class StoreProductReview
{
    public int ProductReviewId { get; set; }

    public int ProductId { get; set; }

    public string ReviewerUid { get; set; } = null!;

    public byte Result { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual StoreProduct Product { get; set; } = null!;
}
