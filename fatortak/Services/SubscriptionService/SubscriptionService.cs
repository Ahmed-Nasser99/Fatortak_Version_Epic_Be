using fatortak.Context;
using fatortak.Dtos.Subscription;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.SubscriptionService
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            ApplicationDbContext context,
            ILogger<SubscriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<SubscriptionDto>> GetAllSubscriptionAsync()
        {
            try
            {
                _logger.LogInformation("Fetching subscriptions");
                var subscriptions = await _context.Subscriptions.ToListAsync();

                

                return subscriptions.Select(s => MapToDto(s)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription");
                throw new ApplicationException($"Failed to get subscription: {ex.Message}", ex);
            }
        }
        
        public async Task<SubscriptionDto> GetSubscriptionAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Fetching subscription with ID: {Id}", id);
                var subscription = await _context.Subscriptions.FindAsync(id);

                if (subscription == null)
                {
                    _logger.LogWarning("Subscription with ID {Id} not found", id);
                    return null;
                }

                return MapToDto(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching subscription with ID: {Id}", id);
                throw new ApplicationException($"Failed to get subscription: {ex.Message}", ex);
            }
        }

        public async Task<SubscriptionDto> GetSubscriptionByTenantAsync(Guid tenantId)
        {
            try
            {
                _logger.LogInformation("Fetching subscription for tenant ID: {TenantId}", tenantId);
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId);

                if (subscription == null)
                {
                    _logger.LogWarning("No subscription found for tenant ID: {TenantId}", tenantId);
                    return null;
                }

                return MapToDto(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching subscription for tenant ID: {TenantId}", tenantId);
                throw new ApplicationException($"Failed to get subscription by tenant: {ex.Message}", ex);
            }
        }

        public async Task<SubscriptionDto> CreateSubscriptionAsync(CreateSubscriptionDto createDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Creating new subscription for tenant ID: {TenantId}", createDto.TenantId);

                var subscription = new Subscription
                {
                    Id = Guid.NewGuid(),
                    TenantId = createDto.TenantId,
                    Plan = createDto.Plan,
                    StartDate = createDto.StartDate,
                    EndDate = createDto.EndDate,
                    IsYearly = createDto.IsYearly,
                    AiUsageThisMonth = 0,
                    AiUsageResetDate = null
                };

                await _context.Subscriptions.AddAsync(subscription);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully created subscription with ID: {Id}", subscription.Id);
                return MapToDto(subscription);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred while creating subscription");
                throw new ApplicationException($"Failed to create subscription: {ex.Message}", ex);
            }
        }

        public async Task<SubscriptionDto> UpdateSubscriptionAsync(Guid id, UpdateSubscriptionDto updateDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Updating subscription with ID: {Id}", id);
                var subscription = await _context.Subscriptions.FindAsync(id);

                if (subscription == null)
                {
                    _logger.LogWarning("Subscription with ID {Id} not found for update", id);
                    return null;
                }

                if (updateDto.Plan.HasValue)
                    subscription.Plan = updateDto.Plan.Value;

                if (updateDto.EndDate.HasValue)
                    subscription.EndDate = updateDto.EndDate.Value;

                if (updateDto.IsYearly.HasValue)
                    subscription.IsYearly = updateDto.IsYearly.Value;

                _context.Subscriptions.Update(subscription);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully updated subscription with ID: {Id}", id);
                return MapToDto(subscription);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred while updating subscription with ID: {Id}", id);
                throw new ApplicationException($"Failed to update subscription: {ex.Message}", ex);
            }
        }

        public async Task DeleteSubscriptionAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Deleting subscription with ID: {Id}", id);
                var subscription = await _context.Subscriptions.FindAsync(id);

                if (subscription == null)
                {
                    _logger.LogWarning("Subscription with ID {Id} not found for deletion", id);
                    return;
                }

                _context.Subscriptions.Remove(subscription);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully deleted subscription with ID: {Id}", id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred while deleting subscription with ID: {Id}", id);
                throw new ApplicationException($"Failed to delete subscription: {ex.Message}", ex);
            }
        }

        public async Task ResetAiUsageAsync(Guid subscriptionId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Resetting AI usage for subscription ID: {SubscriptionId}", subscriptionId);
                var subscription = await _context.Subscriptions.FindAsync(subscriptionId);

                if (subscription == null)
                {
                    _logger.LogWarning("Subscription with ID {SubscriptionId} not found for AI usage reset", subscriptionId);
                    return;
                }

                subscription.AiUsageThisMonth = 0;
                subscription.AiUsageResetDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully reset AI usage for subscription ID: {SubscriptionId}", subscriptionId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred while resetting AI usage for subscription ID: {SubscriptionId}", subscriptionId);
                throw new ApplicationException($"Failed to reset AI usage: {ex.Message}", ex);
            }
        }

        private static SubscriptionDto MapToDto(Subscription subscription)
        {
            if (subscription == null) return null;

            return new SubscriptionDto
            {
                Id = subscription.Id,
                TenantId = subscription.TenantId,
                Plan = subscription.Plan,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                IsYearly = subscription.IsYearly,
                AiUsageThisMonth = subscription.AiUsageThisMonth,
                AiUsageResetDate = subscription.AiUsageResetDate
            };
        }
    }
}
