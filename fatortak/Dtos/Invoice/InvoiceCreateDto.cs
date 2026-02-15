using fatortak.Common.Enum;

namespace fatortak.Dtos.Invoice
{
    public class InvoiceCreateDto
    {
        public Guid CustomerId { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? ProjectId { get; set; }

        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);

        public string? InvoiceType { get; set; } = InvoiceTypes.Sell.ToString();
        public string? Status { get; set; } = InvoiceStatus.Draft.ToString();

        public string? Notes { get; set; }
        public string? Terms { get; set; }

        public List<InvoiceItemCreateDto> Items { get; set; }

        public decimal? DownPayment { get; set; } = 0;
        public int? NumberOfInstallments { get; set; } = 0;
        public decimal? Benefits { get; set; } = 0; // For Only Installment Invoices

        public List<InstallmentCreateDto>? Installments { get; set; }
    }
}
