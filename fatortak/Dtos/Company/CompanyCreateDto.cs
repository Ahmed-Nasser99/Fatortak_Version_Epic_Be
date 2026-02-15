namespace fatortak.Dtos.Company
{
    public class CompanyCreateDto
    {
        public string Name { get; set; }

        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public string? Currency { get; set; } = "USD";
        public decimal DefaultVatRate { get; set; } = 0.2m;
        public string InvoicePrefix { get; set; } = "INV-";
    }
}
