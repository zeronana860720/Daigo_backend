using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class StoreProduct
{
    public int ProductId { get; set; }

    public int StoreId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int Quantity { get; set; }

    public string? Location { get; set; }

    public string? ImagePath { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; }

    public int ReportCount { get; set; }

    public DateTime? LastReportedAt { get; set; }

    public int Status { get; set; }

    public string? RejectReason { get; set; }
    
    // ✨ 新增這兩行
    public int? PlaceId { get; set; }  // 外鍵 (可為 null)
    
    public string? Category { get; set; } 
    
    public virtual StoreProductPlace? Place { get; set; }  // 導覽屬性 (選填)


    public virtual ICollection<BuyerOrderDetail> BuyerOrderDetails { get; set; } = new List<BuyerOrderDetail>();

    public virtual Store Store { get; set; } = null!;

    public virtual ICollection<StoreProductReview> StoreProductReviews { get; set; } = new List<StoreProductReview>();
}
