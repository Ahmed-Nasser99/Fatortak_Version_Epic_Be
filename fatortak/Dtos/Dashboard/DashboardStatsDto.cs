namespace fatortak.Dtos.Dashboard
{
    public class MonthlyFinancialDto
    {
        public string Month { get; set; } // e.g., "Jan", "Feb"
        public int Year { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal Profit { get; set; }
    }

    // Breakdown DTOs for detailed tooltips
    public class RevenueBreakdown
    {
        public decimal Paid { get; set; }
        public decimal PartialPaid { get; set; }
        public decimal Pending { get; set; }
    }

    public class ExpenseBreakdown
    {
        public decimal Paid { get; set; }
        public decimal PartialPaid { get; set; }
        public decimal Pending { get; set; }
    }

    public class DashboardStatsDto
    {
        // Existing fields
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public int TotalInvoices { get; set; }
        public int PaidInvoices { get; set; }
        public decimal PendingAmount { get; set; }
        public int OverdueInvoices { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalSuppliers { get; set; }
        public int TotalItems { get; set; }
        public int ActiveItems { get; set; }

        // New fields for enhanced dashboard
        public decimal CurrentBalance { get; set; }
        public decimal TotalCashAvailable { get; set; }
        public decimal StockValue { get; set; }
        public decimal TotalReceivables { get; set; }
        public decimal TotalPayables { get; set; }

        // Breakdown and date range for tooltips
        public RevenueBreakdown RevenueBreakdown { get; set; }
        public ExpenseBreakdown ExpenseBreakdown { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class RecentInvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; }
        public string CustomerName { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string InvoiceType { get; set; }
    }

    public class TransactionDto
    {
        public Guid Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string Date { get; set; }
        public string Type { get; set; }
        public string Reference { get; set; }
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Remaining { get; set; }
        public Guid? CustomerId { get; set; }
        public string? Status { get; set; }
        public string? TargetId { get; set; }
        public string? Description { get; set; }
        public string? Direction { get; set; }
        
        // Added for Reports sync
        public string? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public string? ProjectName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DashboardResponseDto
    {
        public DashboardStatsDto Stats { get; set; }
        public List<RecentInvoiceDto> RecentInvoices { get; set; }
        public List<TransactionDto> RecentTransactions { get; set; }
        public List<MonthlyFinancialDto> MonthlyFinancials { get; set; } // NEW

    }
}