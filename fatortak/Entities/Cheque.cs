using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    public class Cheque : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TenantId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ChequeNumber { get; set; }

        [Required]
        [MaxLength(200)]
        public string BankName { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime DueDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = ChequeStatus.UnderCollection.ToString();

        [Required]
        public Guid InvoiceId { get; set; }

        // Optional payment account representing the bank account where it will be deposited
        public Guid? PaymentAccountId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(InvoiceId))]
        public Invoice Invoice { get; set; }

        [ForeignKey(nameof(PaymentAccountId))]
        public Account? PaymentAccount { get; set; }

        [ForeignKey(nameof(TenantId))]
        public Tenant Tenant { get; set; }

        // To link the original payment transaction
        public Guid? TransactionId { get; set; }
        public Transaction? Transaction { get; set; }
    }
}
