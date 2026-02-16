namespace fatortak.Dtos.Expense
{
    public class ExpenseFilterDto
    {
        public string? Notes { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? ProjectId { get; set; }
        public string? Category { get; set; }
    }
}
