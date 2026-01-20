using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DemoShopApi.Data;
using DemoShopApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DemoShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // 需要登入才能使用
public class ChatController : ControllerBase
{
    private readonly DaigoContext _context;
    
    public ChatController(DaigoContext context)
    {
        _context = context;
    }
    
    // 設定這個 API 的網址路徑是 "api/Chat/rooms" (GET 請求)
    [HttpGet("rooms")]
    public async Task<IActionResult> GetChatRooms()
    {
        // 1. 從登入憑證 (Token) 中抓取當前使用者的 ID (這個 User 物件是系統自動幫我們解析的)
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // 2. 安全檢查：如果抓不到 ID (可能 Token 過期或沒登入)，就回傳 401 錯誤
        if (userId == null)
        {
            return Unauthorized(); // 告訴前端：「我不認識你，請重新登入」
        }
        
        // --- 修改的部分開始 ---
        
        // 3. 連線到資料庫的 ChatRooms 表格，準備開始撈資料
        var chatRooms = await _context.ChatRooms
            // 4. 過濾條件：只要「UserAid 是我」或者「UserBid 是我」的房間，通通抓出來
            .Where(c => c.UserAid == userId || c.UserBid == userId)
            
            // 5. 投影 (Select)：我們不回傳整包原始資料，而是只挑選我們要的欄位，並重新包裝
            .Select(c => new 
            {
                c.ChatRoomId, // 保留聊天室 ID
                c.CreatedAt,  // 保留建立時間
                
                // 6. 邏輯判斷：到底「對方 (Partner)」是誰？
                // 翻譯：如果 UserAid 等於我自己，那對方就是 UserBid；否則對方就是 UserAid
                PartnerId = c.UserAid == userId ? c.UserBid : c.UserAid,
                
                // 7. 抓取對方名字：
                // 翻譯：如果 UserAid 是我，那我就去抓 UserB 的名字；否則抓 UserA 的名字
                PartnerName = c.UserAid == userId ? c.UserB.Name : c.UserA.Name,
                
                // 8. 抓取對方頭像 (這就是我們要的新功能！)：
                // 翻譯：如果 UserAid 是我，那我就去抓 UserB 的頭像；否則抓 UserA 的頭像
                PartnerAvatar = c.UserAid == userId ? c.UserB.Avatar : c.UserA.Avatar
            })
            
            // 9. 排序：依照建立時間 (CreatedAt) 由新到舊 (Descending) 排列
            .OrderByDescending(x => x.CreatedAt)
            
            // 10. 真正執行 SQL 查詢：把結果轉成 List 列表 (這時候才會真的去資料庫撈資料)
            .ToListAsync();
        // --- 修改的部分結束 ---
        
        // 11. 回傳 200 OK 狀態碼，並把整理好的 chatRooms 資料丟給前端
        return Ok(chatRooms);
    }
    // GET: api/Chat/messages/{chatRoomId}
    [HttpGet("messages/{chatRoomId}")]
    public async Task<IActionResult> GetMessages(string chatRoomId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
        if (userId == null)
        {
            return Unauthorized();
        }
    
        // 驗證使用者是否在這個聊天室中
        var chatRoom = await _context.ChatRooms
            .FirstOrDefaultAsync(c => c.ChatRoomId == chatRoomId && 
                                      (c.UserAid == userId || c.UserBid == userId));
    
        if (chatRoom == null)
        {
            return Forbid();  // 不是這個聊天室的成員
        }
    
        // 取得聊天記錄
        var messages = await _context.ChatMessages
            .Where(m => m.ChatRoomId == chatRoomId)
            .OrderBy(m => m.CreatedAt) // 依照時間排序
            .Select(m => new 
            {
                m.Id,
                m.SenderUserId,
                m.Message,
                m.CreatedAt,
                m.IsRead,
                SenderName = m.Sender != null ? m.Sender.Name : "未知使用者",
                SenderAvatar = m.Sender != null ? m.Sender.Avatar : null
            })
            .ToListAsync();
    
        return Ok(messages);
    }

}