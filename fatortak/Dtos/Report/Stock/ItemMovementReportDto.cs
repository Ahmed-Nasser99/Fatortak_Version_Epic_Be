namespace fatortak.Dtos.Report.Stock
{
    // Item Movement Report DTO
    public class ItemMovementReportDto
    {
        public DateTime Date { get; set; }
        public string InvoiceNumber { get; set; }
        public string Type { get; set; } // "Buy" or "Sell"
        public decimal QtyIn { get; set; }
        public decimal QtyOut { get; set; }
        public decimal Balance { get; set; }
        public decimal UnitPrice { get; set; }
        public int? CurrentBalance { get; set; }
    }
}
