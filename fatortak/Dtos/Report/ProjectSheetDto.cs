using fatortak.Dtos.Transaction;

namespace fatortak.Dtos.Report
{
    public class ProjectSheetDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalReceivables { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public List<TransactionDto> Transactions { get; set; } = new();
    }
}
