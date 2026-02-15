using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.QuotaService
{
    public class QuotaService : IQuotaService
    {
        private readonly ApplicationDbContext _db;

        public QuotaService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<bool> CanCreateInvoiceAsync(Guid tenantId)
        {
            var sub = await GetActiveSubscription(tenantId);

            var limit = sub?.Plan switch
            {
                SubscriptionPlan.Trial => 50,
                SubscriptionPlan.Starter => 100,
                SubscriptionPlan.Professional => 500,
                _ => (int?)null
            };

            if (limit == null) return false;
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            int count = await _db.Invoices.CountAsync(i => i.TenantId == tenantId && i.CreatedAt >= startOfMonth);

            return count < limit;
        }

        public async Task<bool> CanAddCustomerAsync(Guid tenantId) => true;

        public async Task<bool> CanAddItemAsync(Guid tenantId) => true;

        public async Task<bool> CanAddUserAsync(Guid tenantId)
        {
            var sub = await GetActiveSubscription(tenantId);
            var limit = sub?.Plan switch
            {
                SubscriptionPlan.Trial => 1,
                SubscriptionPlan.Starter => 3,
                SubscriptionPlan.Professional => 5,
                _ => (int?)null
            };

            if (limit == null) return false;
            int count = await _db.Users.CountAsync(u => u.TenantId == tenantId);
            return count < limit;
        }

        public async Task<bool> CanUseAiAssistantAsync(Guid tenantId)
        {
            var sub = await GetActiveSubscription(tenantId);
            int? limit = sub?.Plan switch
            {
                SubscriptionPlan.Trial => 10,
                SubscriptionPlan.Starter => 30,
                SubscriptionPlan.Professional => 150,
                SubscriptionPlan.Enterprise => null,
                _ => 0
            };

            return limit == null || sub.AiUsageThisMonth < limit;
        }

        public async Task RecordAiUsageAsync(Guid tenantId)
        {
            var sub = await _db.Subscriptions
                .Where(s => s.TenantId == tenantId && (s.EndDate == null || s.EndDate > DateTime.UtcNow))
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();

            if (sub != null)
            {
                sub.AiUsageThisMonth++;
                await _db.SaveChangesAsync();
            }
        }

        public async Task ResetMonthlyAiUsageAsync()
        {
            var now = DateTime.UtcNow;
            var firstDay = new DateTime(now.Year, now.Month, 1);

            var subs = await _db.Subscriptions
                .Where(s => s.AiUsageResetDate == null || s.AiUsageResetDate < firstDay)
                .ToListAsync();

            foreach (var sub in subs)
            {
                sub.AiUsageThisMonth = 0;
                sub.AiUsageResetDate = now;
            }

            await _db.SaveChangesAsync();
        }

        public async Task<Subscription?> GetActiveSubscription(Guid tenantId)
        {
            return await _db.Subscriptions
                .Where(s => s.TenantId == tenantId && (s.EndDate == null || s.EndDate > DateTime.UtcNow))
                .OrderByDescending(s => s.StartDate)
                .FirstOrDefaultAsync();
        }
    }
}
