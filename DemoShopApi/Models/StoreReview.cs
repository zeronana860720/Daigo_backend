using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class StoreReview
{
    public int ReviewId { get; set; }

    public int ProductId { get; set; }

    public string ReviewerUid { get; set; } = null!;

    public byte Result { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Store Product { get; set; } = null!;
}
