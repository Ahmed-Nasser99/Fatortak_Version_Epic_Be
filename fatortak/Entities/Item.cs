namespace fatortak.Entities
{
    public class Item : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid? BranchId { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string Type { get; set; }
        public int? Quantity { get; set; } = 0;
        public int? InitialQuantity { get; set; } = 0;
        public decimal? UnitPrice { get; set; }
        public decimal? PurchaseUnitPrice { get; set; }
        public string? Unit { get; set; } = "pcs";
        public decimal? VatRate { get; set; }
        public string? ImagePath { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; }
        public Branch? Branch { get; set; }
        public ICollection<InvoiceItem> InvoiceItems { get; set; }
    }
}
