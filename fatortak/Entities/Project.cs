using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    public class Project : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public string Name { get; set; }

        public string? Description { get; set; }

        // Nullable CustomerId allows for Internal Projects
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Active;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ContractValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; } = 0;

        public string? PaymentTerms { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool IncludeVat { get; set; } = false;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal VatRate { get; set; } = 0.14m;

        // Computed property for convenience
        public bool IsInternal => CustomerId == null;

        public Tenant Tenant { get; set; }

        // Navigation properties
        public ICollection<ProjectLine> ProjectLines { get; set; } = new List<ProjectLine>();
        public ICollection<Transaction> Transactions { get; set; }
        public ICollection<Expenses> Expenses { get; set; }
    }
}
