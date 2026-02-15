using fatortak.Common.Enum;
using fatortak.Dtos.Company;

namespace fatortak.Dtos.Invoice
{
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string? InvoiceNumber { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? BranchId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhoneNumber { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public string? Status { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal Total { get; set; }
        public string? Currency { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public string? InvoiceType { get; set; } = InvoiceTypes.Sell.ToString();
        public CompanyDto? Company { get; set; }
        public List<InvoiceItemDto> Items { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public decimal? DownPayment { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? Benefits { get; set; }
        public bool hasInstallments { get; set; } = false;
        public IEnumerable<InstallmentDto> Installments { get; set; } = new List<InstallmentDto>();
        public decimal RemainingAmount => Total - (AmountPaid ?? 0);
    }
}
