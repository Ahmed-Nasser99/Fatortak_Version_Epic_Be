using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    public class FinancialAccount : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required]
        public string Name { get; set; } // e.g., "CIB Main Account", "Office Safe", "Ahmed's Wallet"

        public FinancialAccountType Type { get; set; }

        public string? AccountNumber { get; set; } // For Banks
        public string? BankName { get; set; }
        public string? Iban { get; set; }
        public string? Swift { get; set; }
        public string? Description { get; set; }

        // Reference to an Employee if Type == Custody
        public Guid? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;

        public string? Currency { get; set; } = "EGP";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; }

        // Navigation properties
        public ICollection<Transaction> Transactions { get; set; }
    }
}
