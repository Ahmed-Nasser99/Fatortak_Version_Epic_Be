namespace fatortak.Dtos.Company
{
    public class CompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public string? LogoUrl { get; set; }
        public string? Currency { get; set; }
        public decimal DefaultVatRate { get; set; }
        public string? InvoicePrefix { get; set; }
        public bool? EnableMultipleBranches { get; set; }
        public string? SaleInvoiceTemplate { get; set; }
        public string? SaleInvoiceTemplateColor { get; set; }
        public string? PurchaseInvoiceTemplate { get; set; }
        public string? PurchaseInvoiceTemplateColor { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
