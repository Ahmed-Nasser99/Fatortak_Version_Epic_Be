using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    public class ProjectLine : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public Guid ProjectId { get; set; }
        public Project Project { get; set; }

        [Required]
        public string Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        public string? Unit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
