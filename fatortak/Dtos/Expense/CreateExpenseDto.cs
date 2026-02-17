namespace fatortak.Dtos.Expense
{
    public class CreateExpenseDto
    {
        public DateOnly Date { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public IFormFile? File { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? AccountId { get; set; }
        public string? Category { get; set; }
    }
}
