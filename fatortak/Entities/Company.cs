namespace fatortak.Entities
{
    public class Company : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public string Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public string Currency { get; set; } = "USD";
        public decimal DefaultVatRate { get; set; } = 0.2m;
        public string? InvoicePrefix { get; set; } = "INV-";
        public string? SaleInvoiceTemplate { get; set; } = "modern-gradient";
        public string? SaleInvoiceTemplateColor { get; set; } = "professional-dark";
        public string? PurchaseInvoiceTemplate { get; set; } = "modern-gradient";
        public string? PurchaseInvoiceTemplateColor { get; set; } = "professional-dark";
        public bool EnableMultipleBranches { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; }
        public ICollection<Customer> Customers { get; set; }
        public ICollection<Item> Items { get; set; }
        public ICollection<Invoice> Invoices { get; set; }
    }
}
