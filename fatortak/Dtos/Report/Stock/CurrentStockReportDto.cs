namespace fatortak.Dtos.Report.Stock
{
    public class CurrentStockReportDto
    {
        public Guid ItemId { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public decimal SoldQty { get; set; }
        public int? InStock { get; set; }
        public decimal? PurchasePrice { get; set; }
        public decimal? SellPrice { get; set; }
        public decimal? TotalValue => (InStock ?? 0) * (PurchasePrice ?? 0);
    }
}
