namespace fatortak.Dtos.Invoice
{
    public class MarkPaidDto
    {
        public DateTime? PaidDate { get; set; } = DateTime.UtcNow;
        public string? PaymentMethod { get; set; }
        public string? TransactionReference { get; set; }
    }
}
