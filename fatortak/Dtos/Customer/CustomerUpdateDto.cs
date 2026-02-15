namespace fatortak.Dtos.Customer
{
    public class CustomerUpdateDto
    {
        public string? Name { get; set; }

        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public bool? IsSupplier { get; set; }
        public string? PaymentTerms { get; set; }

        public string? Notes { get; set; }
        public bool IsActive { get; set; }
    }
}
