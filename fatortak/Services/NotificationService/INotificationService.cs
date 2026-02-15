using fatortak.Dtos.Notification;
using fatortak.Dtos.Shared;

namespace fatortak.Services.NotificationService
{
    public interface INotificationService
    {
        Task<ServiceResult<PagedResponseDto<NotificationDto>>> GetNotificationsAsync(
            NotificationFilterDto filter, PaginationDto pagination);

        Task<ServiceResult<bool>> MarkAsReadAsync(Guid notificationId);
        Task<ServiceResult<bool>> MarkAllAsReadAsync();
        Task<ServiceResult<int>> GetUnreadCountAsync();
    }
}