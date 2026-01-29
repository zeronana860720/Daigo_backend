using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class WalletLog
{
    public int Id { get; set; }

    public string Uid { get; set; } = null!;

    public string Action { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal Balance { get; set; }

    public decimal EscrowBalance { get; set; }

    public DateTime CreatedAt { get; set; }

    // ✨ 新增：對應資料庫的 service_code
    public string? ServiceCode { get; set; }

    // ✨ 新增：對應資料庫的 description (用來存委託標題)
    public string? Description { get; set; }
}
