namespace fatortak.Dtos.Invoice
{
    public class RecordPaymentDto
    {
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; } = "Cash";
        public string? AttachmentUrl { get; set; }
        public IFormFile? File { get; set; }
        public Guid? PaymentAccountId { get; set; }

        // Cheque Details
        public string? ChequeNumber { get; set; }
        public string? ChequeBankName { get; set; }
        public DateTime? ChequeDueDate { get; set; }
    }
}
