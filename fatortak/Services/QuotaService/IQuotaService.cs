using fatortak.Entities;

namespace fatortak.Services.QuotaService
{
    public interface IQuotaService
    {
        Task<bool> CanCreateInvoiceAsync(Guid tenantId);
        Task<bool> CanAddCustomerAsync(Guid tenantId);
        Task<bool> CanAddItemAsync(Guid tenantId);
        Task<bool> CanUseAiAssistantAsync(Guid tenantId);
        Task<bool> CanAddUserAsync(Guid tenantId);
        Task RecordAiUsageAsync(Guid tenantId);
        Task ResetMonthlyAiUsageAsync();
        Task<Subscription?> GetActiveSubscription(Guid tenantId);
    }
}
