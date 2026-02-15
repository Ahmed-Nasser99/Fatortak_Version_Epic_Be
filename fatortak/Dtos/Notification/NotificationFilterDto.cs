namespace fatortak.Dtos.Notification
{
    public class NotificationFilterDto
    {
        public bool? IsRead { get; set; }
        public string? NotificationType { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
