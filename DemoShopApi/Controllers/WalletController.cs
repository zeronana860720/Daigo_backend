using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DemoShopApi.Models;
using DemoShopApi.Data;
using DemoShopApi.DTOs;
using Microsoft.EntityFrameworkCore;

namespace DemoShopApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WalletController : ControllerBase
{
    private readonly DaigoContext _context;
    
    public WalletController(DaigoContext context)
    {
        _context = context;
    }
    
    [HttpPost("deposit")]
    [Authorize]
    public async Task<IActionResult> Deposit([FromBody] DepositDto request)
    {
        
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new{message ="金額必須大於0"});
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        
        // 錢包log
        var log = new WalletLog
        {
            Uid = user.Uid,
            Action = "Deposit",
            Amount = request.Amount,
            Balance = (user.Balance??0)+request.Amount,
            EscrowBalance = user.EscrowBalance??0,
            CreatedAt = DateTime.Now
            
        };
        // 紀錄
        _context.WalletLogs.Add(log);   
        user.Balance+= request.Amount;
        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "儲值成功",
            newBalance = user.Balance
        });
    }

    [HttpPost("withdraw")]
    [Authorize]
    public async Task<IActionResult> WithDraw([FromBody] WithdrawDto request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (request.Amount <= 0)
        {
            return BadRequest(new {message = "請求金額需要大於0"});
        }
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        // 檢查餘額 -> 要大於request.amount
        if ((user.Balance ?? 0)< request.Amount)
        {
            return BadRequest(new { message = "餘額不足" });
        }
        // 扣除餘額
        user.Balance -= request.Amount;
        var log = new WalletLog
        {
            Uid = user.Uid,
            Action = "Withdraw",
            Amount = -request.Amount,
            Balance = user.Balance ?? 0,
            EscrowBalance = user.EscrowBalance ?? 0,
            CreatedAt = DateTime.Now
        };
        _context.Add(log);
        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "提現成功",
            newBalance = user.Balance
        });
    }

    [HttpGet("logs")]
    [Authorize]
    public async Task<IActionResult> GetLogs()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var logs = await _context.WalletLogs
            .Where(l => l.Uid == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
        return Ok(logs);
    }
}
