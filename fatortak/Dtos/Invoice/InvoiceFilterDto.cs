namespace fatortak.Dtos.Invoice
{
    public class InvoiceFilterDto
    {
        public string? Search { get; set; }
        public string? InvoiceType { get; set; }
        public Guid? CustomerId { get; set; }
        public string? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public Decimal? minimumPrice { get; set; }
        public Decimal? maximumPrice { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? ProjectId { get; set; }
    }
}
