using fatortak.Entities;

namespace fatortak.Entities
{
    public class Branch : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public bool IsMain { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; }
    }
}
