namespace fatortak.Dtos.Report
{
    public class TopSupplierDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; }
        public int Orders { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime LastOrderDate { get; set; }
    }
}
