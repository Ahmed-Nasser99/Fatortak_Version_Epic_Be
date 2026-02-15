namespace fatortak.Dtos.Customer
{
    public class CustomerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public string? PaymentTerms { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public bool? IsSupplier { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
