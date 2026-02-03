using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DemoShopApi.Hubs;
//[Authorize] // 重要：這會讓 SignalR 自動將連線與 JWT 中的使用者身分 (uid) 綁定

public class NotificationHub : Hub
{
    // 這裡暫時不需要寫邏輯，SignalR 會自動處理 User 對應
}