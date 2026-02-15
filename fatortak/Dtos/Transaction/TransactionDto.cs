namespace fatortak.Dtos.Transaction
{
    public class TransactionDto
    {
        public Guid Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Direction { get; set; }
        public string ReferenceId { get; set; }
        public string ReferenceType { get; set; }
        public string Description { get; set; }
        public string PaymentMethod { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
