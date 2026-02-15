namespace fatortak.Dtos.Report
{
    public class AccountStatementFilterDto
    {
        public Guid CustomerId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string InvoiceType { get; set; } // "Sell" or "Buy"
    }
}
