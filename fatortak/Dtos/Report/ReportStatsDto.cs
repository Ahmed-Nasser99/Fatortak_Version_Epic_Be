namespace fatortak.Dtos.Report
{
    public class ReportStatsDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal? TotalSalaries { get; set; }
        public decimal NetIncome { get; set; }
        public int TotalInvoices { get; set; }
        public int ActiveCustomers { get; set; }
        public int ActiveSuppliers { get; set; }
        public string RevenueChange { get; set; }
        public string ExpensesChange { get; set; }
    }
}
