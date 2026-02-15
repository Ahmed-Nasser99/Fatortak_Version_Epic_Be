using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace fatortak.Entities
{
    public class Payroll : baseEntitiy, ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        
        public int Month { get; set; }
        public int Year { get; set; }
        
        public decimal TotalAmount { get; set; }
        
        public string Status { get; set; } = "Draft"; // Draft, Submitted
        
        public int? ExpenseId { get; set; }
        public Guid? TransactionId { get; set; }
        
        public Tenant Tenant { get; set; }
        public Expenses? Expense { get; set; }
        public Transaction? Transaction { get; set; }
        
        public ICollection<PayrollItem> PayrollItems { get; set; } = new List<PayrollItem>();
    }
}
