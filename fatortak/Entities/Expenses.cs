using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    public class Expenses : ITenantEntity
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public string? FilePath { get; set; }
        public string? OriginalFileName { get; set; }
        public Guid TenantId { get; set; }
        public Guid? BranchId { get; set; }
        
        public Guid? ProjectId { get; set; }
        public Project? Project { get; set; }

        public Guid? CategoryId { get; set; }
        [ForeignKey(nameof(CategoryId))]
        public ExpenseCategory? Category { get; set; }

        public Guid? PaymentAccountId { get; set; }
        [ForeignKey(nameof(PaymentAccountId))]
        public Account? PaymentAccount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; }
        public Branch? Branch { get; set; }
    }
}
