using fatortak.Common.Enum;

namespace fatortak.Entities
{
    public class Invoice : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid? BranchId { get; set; }
        public string InvoiceNumber { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? UserId { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = InvoiceStatus.Draft.ToString();
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal Total { get; set; }
        public string? Currency { get; set; }
        public string? Notes { get; set; }
        public string? Terms { get; set; }
        public string InvoiceType { get; set; } = InvoiceTypes.Sell.ToString();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public decimal? DownPayment { get; set; }   // الدفعة المقدمة (Partial Payment)
        public decimal? AmountPaid { get; set; }    // إجمالي المدفوع حتى الآن
        public decimal? Benefits { get; set; }      // فوائد التقسيط فقط
        public Tenant Tenant { get; set; }
        public Customer Customer { get; set; }
        public ApplicationUser User { get; set; }
        public Branch? Branch { get; set; }
        public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
        public ICollection<Installment>? Installments { get; set; } = new List<Installment>();


        public bool RemindersCreated { get; set; } = false;

    }
}
