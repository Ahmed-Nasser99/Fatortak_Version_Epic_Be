namespace fatortak.Dtos.Report
{
    public class AccountStatementTransactionDto
    {
        public DateTime TransactionDate { get; set; }
        public string Date { get; set; } // Formatted date
        public string TransactionType { get; set; }
        public string TransactionDetails { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public decimal? PaymentAmount { get; set; }
        public decimal? CreditAmount { get; set; }
        public decimal Balance { get; set; }
        public int OrderPriority { get; set; } // For sorting

        public Guid? ProjectId { get; set; }
        public string? ProjectName { get; set; }

        // Export Helpers
        public string Description => TransactionDetails;
        public decimal Debit => InvoiceAmount ?? 0;
        public decimal Credit => (PaymentAmount ?? 0) + (CreditAmount ?? 0);
    }
}
