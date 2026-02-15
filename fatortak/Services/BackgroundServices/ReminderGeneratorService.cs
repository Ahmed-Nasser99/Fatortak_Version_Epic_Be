// fatortak/Services/ReminderService/ReminderGeneratorService.cs
using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace fatortak.Services.ReminderService
{
    public class ReminderGeneratorService : BackgroundService
    {
        private readonly ILogger<ReminderGeneratorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromHours(12); // Run twice daily

        // Hardcoded configuration values
        private const int DueSoonDays = 7;
        private const int OverdueDays = 30;
        private const int RenewalDays = 30;
        private const int InactiveMonths = 3;
        private const int LowStockThreshold = 10;

        public ReminderGeneratorService(
            ILogger<ReminderGeneratorService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reminder Generator Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    await GenerateInvoiceRemindersAsync(dbContext);
                    await GenerateSubscriptionRemindersAsync(dbContext);
                    await GenerateCustomerFollowupRemindersAsync(dbContext);
                    await GenerateInventoryRemindersAsync(dbContext);

                    _logger.LogInformation("Completed reminder generation cycle");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in reminder generator service");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("Reminder Generator Service is stopping.");
        }

        private async Task GenerateInvoiceRemindersAsync(ApplicationDbContext dbContext)
        {
            var today = DateTime.UtcNow.Date;
            var invoices = await dbContext.Invoices
                .Include(i => i.Customer)
                .Where(i => i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                           i.Status == InvoiceStatus.Pending.ToString())
                .ToListAsync();

            foreach (var invoice in invoices)
            {
                var dueDate = invoice.DueDate.Date;

                // Due soon reminder
                if (today == dueDate.AddDays(-DueSoonDays) && invoice.UserId.HasValue)
                {
                    await CreateNotificationAsync(
                        dbContext,
                        invoice.TenantId,
                        invoice.UserId.Value,
                        "Invoice Due Soon",
                        $"Invoice {invoice.InvoiceNumber} is due in {DueSoonDays} days",
                        "InvoiceDue",
                        invoice.Id
                    );
                }

                // Overdue reminders
                if (today > dueDate && today <= dueDate.AddDays(OverdueDays) && invoice.UserId.HasValue)
                {
                    var daysOverdue = (today - dueDate).Days;
                    await CreateNotificationAsync(
                        dbContext,
                        invoice.TenantId,
                        invoice.UserId.Value,
                        "Invoice Overdue",
                        $"Invoice {invoice.InvoiceNumber} is {daysOverdue} days overdue",
                        "InvoiceOverdue",
                        invoice.Id
                    );
                }
            }
        }

        private async Task GenerateSubscriptionRemindersAsync(ApplicationDbContext dbContext)
        {
            var today = DateTime.UtcNow.Date;
            var subscriptions = await dbContext.Subscriptions
                .Where(s => s.EndDate != null && s.EndDate.Value.Date >= today)
                .ToListAsync();

            foreach (var subscription in subscriptions)
            {
                var endDate = subscription.EndDate.Value.Date;

                if (today == endDate.AddDays(-RenewalDays))
                {
                    // Find admin user
                    var admin = await dbContext.Users.FirstOrDefaultAsync(
                        u => u.TenantId == subscription.TenantId &&
                             u.Role == RoleEnum.Admin.ToString());

                    if (admin != null)
                    {
                        await CreateNotificationAsync(
                            dbContext,
                            subscription.TenantId,
                            admin.Id,
                            "Subscription Renewal",
                            $"Your {subscription.Plan} subscription renews in {RenewalDays} days",
                            "SubscriptionRenewal",
                            subscription.Id
                        );
                    }
                }
            }
        }

        private async Task GenerateCustomerFollowupRemindersAsync(ApplicationDbContext dbContext)
        {
            var today = DateTime.UtcNow.Date;
            var thresholdDate = today.AddMonths(-InactiveMonths);

            var customers = await dbContext.Customers
                .Where(c => c.IsActive &&
                           !c.IsDeleted &&
                           !c.IsSupplier &&
                           (c.LastEngagementDate == null || c.LastEngagementDate < thresholdDate))
                .ToListAsync();

            foreach (var customer in customers)
            {
                // Find admin user
                var admin = await dbContext.Users.FirstOrDefaultAsync(
                    u => u.TenantId == customer.TenantId &&
                         u.Role == RoleEnum.Admin.ToString());

                if (admin != null)
                {
                    await CreateNotificationAsync(
                        dbContext,
                        customer.TenantId,
                        admin.Id,
                        "Customer Follow-up",
                        $"{customer.Name} hasn't been engaged with in {InactiveMonths} months",
                        "CustomerFollowup",
                        customer.Id
                    );
                }
            }
        }

        private async Task GenerateInventoryRemindersAsync(ApplicationDbContext dbContext)
        {
            var items = await dbContext.Items
                .Where(i => i.Quantity <= LowStockThreshold)
                .ToListAsync();

            foreach (var item in items)
            {
                // Find admin user
                var admin = await dbContext.Users.FirstOrDefaultAsync(
                    u => u.TenantId == item.TenantId &&
                         u.Role == RoleEnum.Admin.ToString());

                if (admin != null)
                {
                    await CreateNotificationAsync(
                        dbContext,
                        item.TenantId,
                        admin.Id,
                        "Low Stock Alert",
                        $"{item.Name} is low on stock. Current quantity: {item.Quantity}",
                        "LowStock",
                        item.Id
                    );
                }
            }
        }

        private async Task CreateNotificationAsync(
            ApplicationDbContext dbContext,
            Guid tenantId,
            Guid userId,
            string title,
            string message,
            string notificationType,
            Guid? relatedEntityId = null)
        {
            // Check if similar notification already exists today
            var today = DateTime.UtcNow.Date;
            var exists = await dbContext.Notifications.AnyAsync(n =>
                n.TenantId == tenantId &&
                n.UserId == userId &&
                n.NotificationType == notificationType &&
                n.RelatedEntityId == relatedEntityId &&
                n.CreatedAt >= today);

            if (exists) return;

            var notification = new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Title = title,
                Message = message,
                IsRead = false,
                RelatedEntityType = notificationType switch
                {
                    "InvoiceDue" => "Invoice",
                    "InvoiceOverdue" => "Invoice",
                    "SubscriptionRenewal" => "Subscription",
                    "CustomerFollowup" => "Customer",
                    "LowStock" => "Item",
                    _ => null
                },
                RelatedEntityId = relatedEntityId,
                NotificationType = notificationType
            };

            dbContext.Notifications.Add(notification);
            await dbContext.SaveChangesAsync();
        }
    }
}