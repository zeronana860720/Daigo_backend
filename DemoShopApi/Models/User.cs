using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class User
{
    public string Uid { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Phone { get; set; }

    public decimal? Balance { get; set; }

    public decimal? EscrowBalance { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Avatar { get; set; }

    public string? Address { get; set; }
    
    // 失敗幾次就讓他帳號失效
    public DateTime? DisabledUntil { get; set; }
}
