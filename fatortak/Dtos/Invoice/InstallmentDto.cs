namespace fatortak.Dtos.Invoice
{
    public class InstallmentDto
    {
        public Guid Id { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
