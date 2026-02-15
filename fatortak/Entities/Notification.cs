namespace fatortak.Entities
{
    public class Notification : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
        public Guid? RelatedEntityId { get; set; }
        public string RelatedEntityType { get; set; }
        public string NotificationType { get; set; } // "Invoice", "Subscription", etc.

        public Tenant Tenant { get; set; }
        public ApplicationUser User { get; set; }
    }
}
