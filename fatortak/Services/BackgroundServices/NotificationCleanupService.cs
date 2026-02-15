using fatortak.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace fatortak.Services.NotificationService
{
    public class NotificationCleanupService : BackgroundService
    {
        private readonly ILogger<NotificationCleanupService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromDays(7); // Run weekly

        public NotificationCleanupService(
            ILogger<NotificationCleanupService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var threshold = DateTime.UtcNow.AddMonths(-3); // Keep notifications for 3 months
                    var oldNotifications = await dbContext.Notifications
                        .Where(n => n.CreatedAt < threshold)
                        .ToListAsync();

                    dbContext.Notifications.RemoveRange(oldNotifications);
                    await dbContext.SaveChangesAsync();

                    _logger.LogInformation($"Cleaned up {oldNotifications.Count} old notifications");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning up notifications");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("Notification Cleanup Service is stopping.");
        }
    }
}