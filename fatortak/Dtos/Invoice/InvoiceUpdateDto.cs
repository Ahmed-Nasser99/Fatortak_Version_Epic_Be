namespace fatortak.Dtos.Invoice
{
    public class InvoiceUpdateDto
    {
        public Guid? CustomerId { get; set; }
        public Guid? BranchId { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public string? Status { get; set; }
        public string? InvoiceType { get; set; }

        public List<InvoiceItemCreateDto>? Items { get; set; }
        public decimal? DownPayment { get; set; }
        public int? NumberOfInstallments { get; set; }
        public decimal? Benefits { get; set; }
        public List<InstallmentUpdateInvoiceDto>? Installments { get; set; }
    }
    public class InstallmentUpdateInvoiceDto
    {
        public Guid? Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }
}
