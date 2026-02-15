namespace fatortak.Dtos.Report
{
    public class TopCustomerDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; }
        public int Orders { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime LastOrderDate { get; set; }
        public string Status { get; set; }
    }
}
