namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for Profit & Loss (Income Statement) report
    /// </summary>
    public class ProfitAndLossDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<ProfitAndLossItemDto> RevenueItems { get; set; } = new List<ProfitAndLossItemDto>();
        public List<ProfitAndLossItemDto> ExpenseItems { get; set; } = new List<ProfitAndLossItemDto>();
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; } // Revenue - Expenses
    }

    /// <summary>
    /// DTO for a single P&L item
    /// </summary>
    public class ProfitAndLossItemDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal Amount { get; set; }
    }
}

