using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using fatortak.Services.QuotaService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/quota")]
    public class QuotaController : ControllerBase
    {
        private readonly IQuotaService _quotaService;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public QuotaController(IQuotaService quotaService, IHttpContextAccessor contextAccessor, ApplicationDbContext context)
        {
            _quotaService = quotaService;
            _httpContextAccessor = contextAccessor;
            _context = context;
        }
        private Guid _tenantId =>
    ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        [HttpGet("can-create-invoice")]
        public async Task<IActionResult> CanCreateInvoice()
        {
            var allowed = await _quotaService.CanCreateInvoiceAsync(_tenantId);
            return Ok(new { allowed });
        }

        [HttpGet("can-use-ai")]
        public async Task<IActionResult> CanUseAi()
        {
            var allowed = await _quotaService.CanUseAiAssistantAsync(_tenantId);
            return Ok(new { allowed });
        }

        [HttpGet("can-add-user")]
        public async Task<IActionResult> CanAddUser()
        {
            var allowed = await _quotaService.CanAddUserAsync(_tenantId);
            return Ok(new { allowed });
        }

        [HttpGet("usage")]
        public async Task<IActionResult> GetQuotaUsage()
        {
            var sub = await _quotaService.GetActiveSubscription(_tenantId);

            if (sub is null)
            {
                return StatusCode(430);
            }

            int? aiLimit = sub.Plan switch
            {
                SubscriptionPlan.Trial => 10,
                SubscriptionPlan.Starter => 30,
                SubscriptionPlan.Professional => 150,
                SubscriptionPlan.Enterprise => null,
                _ => 0
            };

            int? invoiceLimit = sub.Plan switch
            {
                SubscriptionPlan.Trial => 50,
                SubscriptionPlan.Starter => 100,
                SubscriptionPlan.Professional => 500,
                SubscriptionPlan.Enterprise => null,
                _ => 0
            };

            int? userLimit = sub.Plan switch
            {
                SubscriptionPlan.Trial => 1,
                SubscriptionPlan.Starter => 3,
                SubscriptionPlan.Professional => 5,
                SubscriptionPlan.Enterprise => null,
                _ => 0
            };

            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var invoiceCount = await _context.Invoices.CountAsync(i => i.TenantId == _tenantId && i.CreatedAt >= startOfMonth);
            var userCount = await _context.Users.CountAsync(u => u.TenantId == _tenantId);

            return Ok(new
            {
                plan = sub.Plan.ToString(),
                startDate = sub.StartDate,
                endDate = sub.EndDate,
                isYearly = sub.IsYearly,

                aiUsed = sub.AiUsageThisMonth,
                aiLimit,
                remainingAi = aiLimit != null ? aiLimit - sub.AiUsageThisMonth : null,

                invoicesThisMonth = invoiceCount,
                invoiceLimit,
                remainingInvoices = invoiceLimit != null ? invoiceLimit - invoiceCount : null,

                users = userCount,
                userLimit,
                remainingUsers = userLimit != null ? userLimit - userCount : null
            });
        }
    }


}
