namespace fatortak.Dtos.Transaction
{
    public class TransactionDto
    {
        public Guid Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Date { get; set; } // Formatted date
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Direction { get; set; }
        public string? ReferenceId { get; set; }
        public string? Reference { get; set; } // Alias for ReferenceId or description
        public string? ReferenceType { get; set; }
        public string? Description { get; set; }
        public string? PaymentMethod { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }

        public Guid? FinancialAccountId { get; set; }
        public string? FinancialAccountName { get; set; }

        public Guid? CounterpartyAccountId { get; set; }
        public string? CounterpartyAccountName { get; set; }

        public string? AttachmentUrl { get; set; }
        public string? Category { get; set; }
    }
}
