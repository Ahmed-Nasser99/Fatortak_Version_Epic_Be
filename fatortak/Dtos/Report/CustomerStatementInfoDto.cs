namespace fatortak.Dtos.Report
{
    public class CustomerStatementInfoDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string AccountType { get; set; } // "Customer" or "Supplier"
        public string Currency { get; set; }
    }
}
