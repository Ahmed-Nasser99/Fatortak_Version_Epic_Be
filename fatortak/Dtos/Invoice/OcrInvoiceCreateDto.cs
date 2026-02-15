using fatortak.Common.Enum;

namespace fatortak.Dtos.Invoice
{
    public class OcrInvoiceCreateDto
    {
        public Guid? CustomerId { get; set; }
        public Guid? BranchId { get; set; }
        public string? SallerName { get; set; }
        public string? SallerEmail { get; set; }
        public string? SallerPhone { get; set; }
        public string? SallerAddress { get; set; }
        public string? SallerTaxNumber { get; set; }
        public string? SallerVATNumber { get; set; }
        public string? BuyerName { get; set; }
        public string? BuyerEmail { get; set; }
        public string? BuyerPhone { get; set; }
        public string? BuyerAddress { get; set; }
        public string? BuyerTaxNumber { get; set; }
        public string? BuyerVATNumber { get; set; }

        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);

        public string? InvoiceType { get; set; } = InvoiceTypes.Sell.ToString();
        public string? Status { get; set; } = InvoiceStatus.Draft.ToString();

        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public string? PurchaseType { get; set; }

        public List<OcrInvoiceItemCreateDto> Items { get; set; }

        public decimal? DownPayment { get; set; } = 0;
        public int? NumberOfInstallments { get; set; } = 0;
        public decimal? Benefits { get; set; } = 0; // For Only Installment Invoices

        public List<InstallmentCreateDto>? Installments { get; set; }
    }
}
