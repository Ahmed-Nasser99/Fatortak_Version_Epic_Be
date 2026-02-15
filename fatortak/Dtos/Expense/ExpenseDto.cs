namespace fatortak.Dtos.Expense
{
    public class ExpenseDto
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public Guid? BranchId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
