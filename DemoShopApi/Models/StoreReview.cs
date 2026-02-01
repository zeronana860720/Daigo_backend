using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class StoreReview
{
    public int ReviewId { get; set; }

    // 完全不要有 ProductId 或 StoreId
    // public int? ProductId { get; set; }  
    public int? StoreId { get; set; }

    public string ReviewerUid { get; set; } = null!;

    public byte Result { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    // 也不要有導覽屬性
    // public virtual Store? Product { get; set; }
    // public virtual Store? Store { get; set; }
}