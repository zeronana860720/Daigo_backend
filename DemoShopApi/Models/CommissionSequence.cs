using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class CommissionSequence
{
    public string Ym { get; set; } = null!;

    public int Seq { get; set; }

    public DateTime UpdatedAt { get; set; }
}
