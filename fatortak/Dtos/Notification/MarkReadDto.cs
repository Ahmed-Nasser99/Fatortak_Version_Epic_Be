namespace fatortak.Dtos.Notification
{
    public class MarkReadDto
    {
        public bool MarkAll { get; set; }
        public Guid? NotificationId { get; set; }
    }
}
