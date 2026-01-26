using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class Commission
{
    public int CommissionId { get; set; }

    public string ServiceCode { get; set; } = null!;

    public string? CreatorId { get; set; }

    public string? Title { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public int? Quantity { get; set; }

    public string? Category { get; set; }

    public string? Location { get; set; }

    public DateTime? Deadline { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string Status { get; set; } = null!;

    public decimal? EscrowAmount { get; set; }

    public decimal? Fee { get; set; }

    public int? FailCount { get; set; }

    public int? PlaceId { get; set; }
    // 貨幣欄位
    public string Currency { get; set; } = "TWD";

    public virtual ICollection<CommissionHistory> CommissionHistories { get; set; } = new List<CommissionHistory>();

    public virtual ICollection<CommissionOrder> CommissionOrders { get; set; } = new List<CommissionOrder>();

    public virtual ICollection<CommissionReceipt> CommissionReceipts { get; set; } = new List<CommissionReceipt>();

    public virtual ICollection<CommissionShipping> CommissionShippings { get; set; } = new List<CommissionShipping>();

    public virtual CommissionPlace? Place { get; set; }
    
    public virtual User? User { get; set; } // 這是導覽屬性，讓 Include 可以運作
}
