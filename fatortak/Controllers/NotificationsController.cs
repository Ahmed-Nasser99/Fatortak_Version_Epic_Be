using fatortak.Dtos.Notification;
using fatortak.Dtos.Shared;
using fatortak.Services.NotificationService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService notificationService,
            ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<NotificationDto>>>> GetNotifications(
            [FromQuery] NotificationFilterDto filter, [FromQuery] PaginationDto pagination)
        {
            var result = await _notificationService.GetNotificationsAsync(filter, pagination);
            return HandleServiceResult(result);
        }

        [HttpPost("{id}/read")]
        public async Task<ActionResult<ServiceResult<bool>>> MarkAsRead(Guid id)
        {
            var result = await _notificationService.MarkAsReadAsync(id);
            return HandleServiceResult(result);
        }

        [HttpPost("read-all")]
        public async Task<ActionResult<ServiceResult<bool>>> MarkAllAsRead()
        {
            var result = await _notificationService.MarkAllAsReadAsync();
            return HandleServiceResult(result);
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<ServiceResult<int>>> GetUnreadCount()
        {
            var result = await _notificationService.GetUnreadCountAsync();
            return HandleServiceResult(result);
        }

        private ActionResult HandleServiceResult<T>(ServiceResult<T> result)
        {
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
    }
}