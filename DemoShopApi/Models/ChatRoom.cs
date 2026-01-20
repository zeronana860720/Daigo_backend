using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class ChatRoom
{
    public string ChatRoomId { get; set; } = null!;

    public string UserAid { get; set; } = null!;

    public string UserBid { get; set; } = null!;

    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    // --- 補上這兩行 ---
    public virtual User UserA { get; set; } = null!;
    public virtual User UserB { get; set; } = null!;
}
