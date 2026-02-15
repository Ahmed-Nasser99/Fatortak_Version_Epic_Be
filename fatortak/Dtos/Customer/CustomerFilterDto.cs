namespace fatortak.Dtos.Customer
{
    public class CustomerFilterDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsSupplier { get; set; }
    }
}
