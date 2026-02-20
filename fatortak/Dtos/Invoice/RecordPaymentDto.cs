namespace fatortak.Dtos.Invoice
{
    public class RecordPaymentDto
    {
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; } = "Cash";
        public string? AttachmentUrl { get; set; }
        public IFormFile? File { get; set; }
    }
}
