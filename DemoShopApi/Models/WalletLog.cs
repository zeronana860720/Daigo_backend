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
}
