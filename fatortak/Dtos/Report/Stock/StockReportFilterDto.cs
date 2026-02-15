namespace fatortak.Dtos.Report.Stock
{
    // Filter DTOs
    public class StockReportFilterDto
    {
        public string? Search { get; set; }
        public Guid? ItemId { get; set; }
        public bool? LowStock { get; set; } // Items below minimum stock level
    }
}
