namespace fatortak.Entities
{
    public class Expenses : ITenantEntity
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public string? FilePath { get; set; }
        public string? OriginalFileName { get; set; }
        public Guid TenantId { get; set; }
        public Guid? BranchId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; }
        public Branch? Branch { get; set; }
    }
}
