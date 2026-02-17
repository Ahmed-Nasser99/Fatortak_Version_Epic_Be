namespace fatortak.Entities
{
    public class Customer : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSupplier { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public string? PaymentTerms { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; }
        public ICollection<Invoice> Invoices { get; set; }
        public DateTime? LastEngagementDate { get; set; }

        public Guid? AccountId { get; set; }
        public Account? Account { get; set; }
    }
}
