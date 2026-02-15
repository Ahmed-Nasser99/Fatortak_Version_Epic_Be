using fatortak.Common.Enum;

namespace fatortak.Entities
{
    public class Installment : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid InvoiceId { get; set; }
        public DateTime DueDate { get; set; }   
        public decimal Amount { get; set; }        
        public string Status { get; set; } = InstallmentStatus.Unpaid.ToString();
        public DateTime? PaidAt { get; set; }

        public Invoice Invoice { get; set; }
    }
}
