namespace fatortak.Dtos.Notification
{
    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public Guid? RelatedEntityId { get; set; }
        public string RelatedEntityType { get; set; }
        public string NotificationType { get; set; }
    }
}
