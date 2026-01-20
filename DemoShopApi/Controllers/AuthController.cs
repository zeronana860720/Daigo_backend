using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using DemoShopApi.Data;
using DemoShopApi.DTOs;
using DemoShopApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DemoShopApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly DaigoContext _context;
    private readonly IConfiguration _configuration;
    // _configuration -> 可以去設定裡面拿東西的人

    // 透過建構子注入資料庫上下文 (DbContext)
    public AuthController(DaigoContext context,IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(UserRegisterDto request)
    {
        // 1. 檢查 Email 是否已被註冊
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest("該 Email 已經被註冊過囉！");
        }

        // 2. 使用 BCrypt 對密碼進行加密 (不可逆雜湊)
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // 3. 建立新的 User 實體物件
        var user = new User
        {
            Uid = Guid.NewGuid().ToString(), // 自動生成唯一識別碼
            Name = request.Name,
            Email = request.Email,
            PasswordHash = passwordHash,
            Phone = request.Phone,
            Balance = 0,
            EscrowBalance = 0,
            CreatedAt = DateTime.Now
        };

        // 4. 存入資料庫
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "註冊成功！" });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(UserLoginDto request)
    {
        // 1. 尋找使用者
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            return BadRequest("找不到此用戶");
        }

        // 2. 比對加密密碼
        // 使用 BCrypt 驗證前端傳來的明文密碼與資料庫裡的 Hash 是否一致
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return BadRequest("帳號或密碼錯誤");
        }

        // 3. 密碼正確，發放 JWT Token
        string token = CreateToken(user);

        // 回傳 Token 以及一些基本資訊給前端
        return Ok(new { 
            token = token,
            name = user.Name,
            email = user.Email,
            avatar = user.Avatar,
            balance = user.Balance,
            userId = user.Uid, 
        });
    }
    [HttpGet("profile")]
    [Authorize] // 必須登入才能看
    public async Task<IActionResult> GetProfile()
    {
        // 1. 從 Token 中抓出目前登入者的 Uid
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null) return Unauthorized();
        // 這一段是驗證請求的合法性

        // 2. 去資料庫找這個人  
        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound();
        // 這一段是在驗證資料完整性-> 不會有可以請求但是uid找不到人的情況

        // 3. 回傳資料-> 絕對不能回傳密碼
        // 前端需要的東西
        return Ok(new
        {
            user.Name,
            user.Email,
            user.Phone,
            user.Address,
            user.Avatar,
            user.Balance // 也可以順便回傳餘額
        });
    }
    [HttpPost("update")]
    [Authorize] // 
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileDto dto)
    {
        // 1. 從 Token 拿到目前登入者的 Uid -> 確認身份
        
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _context.Users.FindAsync(userId);

        if (user == null) return NotFound("找不到使用者");

        // 2. 更新文字欄位
        user.Phone = dto.Phone;
        user.Address = dto.Address;

        // 3. 處理頭像上傳 (如果有傳檔案過來的話)
        if (dto.AvatarFile != null && dto.AvatarFile.Length > 0)
        {
            // A. 決定存檔路徑 (存到 wwwroot/uploads 資料夾)
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            // B. 產生不重複的檔名 (避免大家檔名都叫 me.jpg 會覆蓋)
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.AvatarFile.FileName)}";
            var filePath = Path.Combine(folderPath, fileName); // (路徑,檔名)
            

            // C. 真正把檔案寫入硬碟
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.AvatarFile.CopyToAsync(stream);
            }
            /*
             * FileStream -> 檔案流:類似水管的功能,程式和硬碟之中的傳送館
             * filePath -> 路徑和檔名,剛剛弄好的
             * 在這個地址幫我弄一個新的檔案
             * await dto.AvatarFile.CopyToAsync(stream);
               -> dto.AvatarFile :前端給的東西
               -> CopyToAsync 把前端傳回來的二進位數據灌進剛剛挖好的地址
               二進位地址是怎麼在硬碟裡面變成圖片的?
               
             */

            // D. 把資料庫裡的 Avatar 欄位改成圖片的網址路徑
            user.Avatar = $"/uploads/{fileName}";
        }

        // 4. 儲存變更到資料庫
        await _context.SaveChangesAsync();

        return Ok(new { message = "success", avatarUrl = user.Avatar });
        // 為什麼要再返回一次avatarUrl -> 這樣就不需要再使用api呼叫來更新圖片
        // 手機和地址不用返回,前後一致沒有暫時性問題
    }

    [HttpGet("wallet")]
    [Authorize]
    public async Task<IActionResult> GetWalletInfo()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }
        
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        return Ok(new
            {
                availableBalance=user.Balance,
                escrowBalance=user.EscrowBalance
            }
        );
    }

    // 產生 JWT Token 的核心工具
    private string CreateToken(User user)
    {
        // 設定「聲明 (Claims)」：這是 Token 裡面攜帶的資訊
        // 這個聲明是一個陣列
        var claims = new List<Claim>
        {
            // 使用ClaimTypes -> 確保大家用的是同一個標準,才不會字串都亂打
            new Claim(ClaimTypes.Name, user.Name),
            // 第一個參數 (Type)：告訴系統這筆資料「是什麼」類型（例如：這是姓名？還是 Email？）
            // 第二個參數 (Value)：這筆資料「具體的值」是什麼（例如：MOMO）
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Uid) // 將 Uid 存入 Token
            
            // 技術上其實可以只Claim NameIdentifier 也可以登入
            // 但是這樣要用到名字的時候就必須要用api 前端來講很牙給
        };

        // 從 appsettings.json 讀取密鑰
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration.GetSection("Jwt:Key").Value!));
        // _configuration.GetSection("Jwt:Key").Value! -> 讀取秘密字串
        // Encoding.UTF8.GetBytes -> 翻譯成電腦懂的樣子
        // SymmetricSecurityKey -> 對稱金鑰,加密解密都用同一個鑰匙
        // Value! -> 保證不是null

        // 設定簽署憑證（使用 HmacSha512 演算法）
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        // 建立 Token 內容
        var tokenDescriptor = new JwtSecurityToken(
            issuer: _configuration.GetSection("Jwt:Issuer").Value,
            audience: _configuration.GetSection("Jwt:Audience").Value,
            claims: claims,
            expires: DateTime.Now.AddDays(1), // Token 有效期為 1 天
            signingCredentials: creds
        );

        // 產出字串形式的 JWT
        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }
}