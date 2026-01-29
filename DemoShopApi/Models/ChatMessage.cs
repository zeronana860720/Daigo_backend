using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class ChatMessage
{
    public int Id { get; set; }

    public string ChatRoomId { get; set; } = null!;

    public string SenderUserId { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }
    // --- 補上這行 ---
    public virtual User Sender { get; set; } = null!;
}
