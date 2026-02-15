using fatortak.Context;
using fatortak.Dtos.Notification;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace fatortak.Services.NotificationService
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        private Guid UserId
        {
            get
            {
                var userId = UserHelper.GetUserId();
                return string.IsNullOrEmpty(userId) ? Guid.Empty : new Guid(userId);
            }
        }

        public async Task<ServiceResult<PagedResponseDto<NotificationDto>>> GetNotificationsAsync(
            NotificationFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.Notifications
                    .Where(n => n.TenantId == TenantId && n.UserId == UserId)
                    .OrderByDescending(n => n.CreatedAt)
                    .AsQueryable();

                if (filter.IsRead.HasValue)
                    query = query.Where(n => n.IsRead == filter.IsRead.Value);

                if (filter.NotificationType != null)
                    query = query.Where(n => n.NotificationType == filter.NotificationType);

                if (filter.FromDate.HasValue)
                    query = query.Where(n => n.CreatedAt >= filter.FromDate.Value);

                if (filter.ToDate.HasValue)
                    query = query.Where(n => n.CreatedAt <= filter.ToDate.Value);

                var totalCount = await query.CountAsync();

                var notifications = await query
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var notificationDtos = notifications.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<NotificationDto>>.SuccessResult(
                    new PagedResponseDto<NotificationDto>
                    {
                        Data = notificationDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications");
                return ServiceResult<PagedResponseDto<NotificationDto>>.Failure("Failed to retrieve notifications");
            }
        }

        public async Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == notificationId &&
                                            n.TenantId == TenantId &&
                                            n.UserId == UserId);

                if (notification == null)
                    return ServiceResult<bool>.Failure("Notification not found");

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking notification {notificationId} as read");
                return ServiceResult<bool>.Failure("Failed to mark notification as read");
            }
        }

        public async Task<ServiceResult<bool>> MarkAllAsReadAsync()
        {
            try
            {
                var unreadNotifications = await _context.Notifications
                    .Where(n => n.TenantId == TenantId &&
                               n.UserId == UserId &&
                               !n.IsRead)
                    .ToListAsync();

                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                    notification.ReadAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read");
                return ServiceResult<bool>.Failure("Failed to mark all notifications as read");
            }
        }

        public async Task<ServiceResult<int>> GetUnreadCountAsync()
        {
            try
            {
                var count = await _context.Notifications
                    .CountAsync(n => n.TenantId == TenantId &&
                                    n.UserId == UserId &&
                                    !n.IsRead);

                return ServiceResult<int>.SuccessResult(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count");
                return ServiceResult<int>.Failure("Failed to get unread count");
            }
        }

        private NotificationDto MapToDto(Notification notification) => new()
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            RelatedEntityId = notification.RelatedEntityId,
            RelatedEntityType = notification.RelatedEntityType,
            NotificationType = notification.NotificationType
        };
    }
}