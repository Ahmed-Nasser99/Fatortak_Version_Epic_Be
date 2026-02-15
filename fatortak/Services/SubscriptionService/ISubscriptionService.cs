using fatortak.Dtos.Subscription;

namespace fatortak.Services.SubscriptionService
{
    public interface ISubscriptionService
    {
        Task<List<SubscriptionDto>> GetAllSubscriptionAsync();
        Task<SubscriptionDto> GetSubscriptionAsync(Guid id);
        Task<SubscriptionDto> GetSubscriptionByTenantAsync(Guid tenantId);
        Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionDto createDto);
        Task<SubscriptionDto> UpdateSubscriptionAsync(Guid id, UpdateSubscriptionDto updateDto);
        Task DeleteSubscriptionAsync(Guid id);
        Task ResetAiUsageAsync(Guid subscriptionId);
    }
}
