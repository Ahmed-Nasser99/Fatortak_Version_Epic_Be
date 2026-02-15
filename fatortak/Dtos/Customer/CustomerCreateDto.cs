using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Customer
{
    public class CustomerCreateDto
    {
        public string Name { get; set; }
        public string? Email { get; set; }

        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TaxNumber { get; set; }
        public string? VATNumber { get; set; }
        public string? PaymentTerms { get; set; }
        public bool IsSupplier { get; set; }
        public string? Notes { get; set; }
    }
}
