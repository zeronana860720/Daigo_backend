using DemoShopApi.DTOs;
using DemoShopApi.Models;
using DemoShopApi.services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using DemoShopApi.Data;


namespace DemoShopApi.Controllers
{
    [ApiController]
    [Route("admin")]
    public class AdminCommissionController : ControllerBase
    {
        private readonly DaigoContext _proxyContext;
        private readonly ReviewService _RVservice;
        public AdminCommissionController(DaigoContext proxyContext,ReviewService RVservice)
        {
            _proxyContext = proxyContext;
            _RVservice = RVservice;
        }
        [HttpGet("History")]
        public async Task<IActionResult> SearchHistoryALL()
        {
            var userid = "administrator";
            var user = await _proxyContext.Users
                       .FirstOrDefaultAsync(c => c.Name == userid);
            if (user == null && userid != "administrator")
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "尚未登入，或是權限不足"
                });
            }

            var History = await _proxyContext.CommissionHistories
                          .OrderBy(c => c.CommissionId)
                          .Select(c => new
                          {
                              historyid = c.HistoryId,
                              commissionid = c.CommissionId,
                              action = c.Action,
                              changedby = c.ChangedBy,
                              changedAt = c.ChangedAt,
                              oldData = c.OldData,
                              newData = c.NewData,
                          }).ToListAsync();
            return Ok(
                new
                {
                    success = true,
                    data = History
                });
        }
        [HttpGet("History/{ServiceCode}")]
        public async Task<IActionResult> SearchHistoryOnly(String ServiceCode)
        {
            var userid = "administrator";
            var user = await _proxyContext.Users
                       .FirstOrDefaultAsync(c => c.Name == userid);
            if (user == null && userid != "administrator")
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "尚未登入，或是權限不足"
                });
            }
            var commissionid = await _proxyContext.Commissions
                    .Where(c => c.ServiceCode == ServiceCode)
                    .Select(c => c.CommissionId)
                    .FirstOrDefaultAsync();
            if (commissionid == 0)
            {
                return NotFound(new
                {
                    success = false,
                    message = "找不到委託"
                });
            }
            var History = await _proxyContext.CommissionHistories
                         .Where(c => c.CommissionId == commissionid)
                         .OrderBy(c => c.ChangedAt)
                         .Select(c => new
                         {
                             historyid = c.HistoryId,
                             commissionid = c.CommissionId,
                             action = c.Action,
                             changedby = c.ChangedBy,
                             changedAt = c.ChangedAt,
                             oldData = c.OldData,
                             newData = c.NewData,
                         }).ToListAsync();
            return Ok(
                new
                {
                    success = true,
                    data = History
                });

        }

        // 依照{commission/product} 查詢 {流水號} 找審核紀錄
        [HttpGet("{targetType}/{TargetCode}")]
        public async Task<IActionResult> Get(string targetType, string TargetCode)
        {
            var result = await _RVservice.GetReviewsByTargetCode(targetType, TargetCode);
            return Ok(result);
        }

        //撈審核清單
        [HttpGet("Review/Pending")]
        public async Task<IActionResult> GetPending()
        {
            return Ok(await _RVservice.GetPendingCommissionsForReview());
        }

        // 審核
        [HttpPost("Review/Pending")]
        public async Task<IActionResult> Review([FromBody] ReviewRequestDto req)
        {
            var reviewerUid = User.FindFirstValue(ClaimTypes.NameIdentifier)??"admin";
            
            await _RVservice.Review(req, reviewerUid);
            return Ok();
        }

    }
}
