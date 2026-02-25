namespace fatortak.Dtos.Cheque
{
    public class ChequeDto
    {
        public Guid Id { get; set; }
        public string ChequeNumber { get; set; }
        public string BankName { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string? ProjectName { get; set; }
        public Guid? PaymentAccountId { get; set; }
        public string? PaymentAccountName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
