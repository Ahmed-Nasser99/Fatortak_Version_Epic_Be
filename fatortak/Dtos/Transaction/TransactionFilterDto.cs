namespace fatortak.Dtos.Transaction
{
    public class TransactionFilterDto
    {
        public string? Search { get; set; }
        public string? Type { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public string? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public Guid? ProjectId { get; set; }
        public string? Category { get; set; }
    }
}
