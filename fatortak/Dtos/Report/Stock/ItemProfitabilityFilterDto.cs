namespace fatortak.Dtos.Report.Stock
{
    public class ItemProfitabilityFilterDto
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Search { get; set; }
        public int? TopCount { get; set; } // Get top N profitable items
    }
}
