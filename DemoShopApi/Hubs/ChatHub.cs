using Microsoft.AspNetCore.SignalR;
using DemoShopApi.Models;
using DemoShopApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DemoShopApi.Hubs;

public class ChatHub : Hub
{
    private readonly DaigoContext _context;
    
    public ChatHub(DaigoContext context)
    {
        _context = context;
    }
    
    private string GetCurrentUserId()
    {
        
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
        if (userId == null)
        {
            throw new HubException("無法取得使用者 ID");
        }
    
        return userId;
    }

    // 使用者上線時,加入他所有的聊天室
    public async Task ConnectUser()
    {
        // 找使用者Id
        var userId = GetCurrentUserId();
        
        // 找他到底參加了哪些聊天室（資料庫）
        var chatRooms = await _context.ChatRooms
            .Where(c => c.UserAid == userId || c.UserBid == userId)
            .Select(c => c.ChatRoomId)
            .ToListAsync();

        // 綁定群組
        foreach (var roomId in chatRooms)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }
        // SignalR-> 大樓管理員 你要進入會給你一張訪客證可能號碼是 #999
        // roomId ->我是來參加A,B社團的活動
        // 變成編號999的訪客可以參加A,B社團的活動
    }

    // 發送訊息 (一對一)
    public async Task SendMessage(string chatRoomId, string message)
    {
        // 驗證使用者身份
        var userId = GetCurrentUserId();  
        
        // 開始看你有沒有進入聊天室的資格
        // lambda表達式-> 開始一個一個看_context.ChatRooms裡面每一間房間 一間一間看有沒有符合的
        var chatRoom = await _context.ChatRooms
            .FirstOrDefaultAsync(c => c.ChatRoomId == chatRoomId && 
                                     (c.UserAid == userId || c.UserBid == userId));

        if (chatRoom == null)
        {
            throw new HubException("您不在此聊天室中");
        }

        // 儲存訊息到資料庫
        var chatMessage = new ChatMessage
        {
            ChatRoomId = chatRoomId,
            SenderUserId = userId,
            Message = message,
            CreatedAt = DateTime.Now,
            IsRead = false
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        // 只廣播給這個聊天室的兩個人
        await Clients.Group(chatRoomId).SendAsync("ReceiveMessage", new
        {
            id = chatMessage.Id,
            chatRoomId = chatMessage.ChatRoomId,
            senderUserId = chatMessage.SenderUserId,
            message = chatMessage.Message,
            createdAt = chatMessage.CreatedAt
        });
    }

    // 標記訊息為已讀
    public async Task MarkAsRead(string chatRoomId)
    {
        var userId = GetCurrentUserId();  // 從 Token 取得使用者 ID
        
        var messages = await _context.ChatMessages
            .Where(m => m.ChatRoomId == chatRoomId && 
                       m.SenderUserId != userId && 
                       !m.IsRead)
            .ToListAsync();

        foreach (var message in messages)
        {
            message.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }
}
