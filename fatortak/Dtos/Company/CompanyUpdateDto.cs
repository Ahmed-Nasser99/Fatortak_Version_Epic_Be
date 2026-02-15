namespace fatortak.Dtos.Company
{
    public class CompanyUpdateDto
    {
        public string Name { get; set; }

        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public string? Currency { get; set; }
        public decimal? DefaultVatRate { get; set; }
        public string? InvoicePrefix { get; set; }
        public bool? EnableMultipleBranches { get; set; }
    }
}
