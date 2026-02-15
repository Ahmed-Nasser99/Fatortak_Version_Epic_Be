namespace fatortak.Dtos.Report.Stock
{
    // Item Profitability Report DTO
    public class ItemProfitabilityReportDto
    {
        public Guid ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public decimal TotalSales { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitPercentage { get; set; }
    }
}
