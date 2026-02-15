namespace fatortak.Dtos.Report.Stock
{
    public class ItemMovementFilterDto
    {
        public Guid ItemId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
