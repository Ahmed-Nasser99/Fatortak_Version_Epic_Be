using System;

namespace fatortak.Dtos.Expense
{
    public class ExpenseFilterDto
    {
        public string? Notes { get; set; }
        public Guid? BranchId { get; set; }
    }
}
