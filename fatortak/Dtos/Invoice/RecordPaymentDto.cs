namespace fatortak.Dtos.Invoice
{
    public class RecordPaymentDto
    {
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; } = "Cash";
    }
}
