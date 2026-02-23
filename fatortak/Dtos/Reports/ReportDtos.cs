namespace fatortak.Dtos.Reports
{
    public class ReportBaseDto
    {
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string Title { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    #region Financial Reports

    public class TrialBalanceDto : ReportBaseDto
    {
        public List<TrialBalanceItemDto> Items { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public bool IsBalanced { get; set; }
    }

    public class TrialBalanceItemDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal OpeningDebit { get; set; }
        public decimal OpeningCredit { get; set; }
        public decimal PeriodDebit { get; set; }
        public decimal PeriodCredit { get; set; }
        public decimal ClosingDebit { get; set; }
        public decimal ClosingCredit { get; set; }
        public decimal NetBalance { get; set; }
    }

    public class LedgerDto : ReportBaseDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<LedgerEntryDto> Entries { get; set; } = new();
    }

    public class LedgerEntryDto
    {
        public Guid JournalEntryId { get; set; }
        public string EntryNumber { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
        public string Reference { get; set; }
    }

    public class IncomeStatementDto : ReportBaseDto
    {
        public List<IncomeStatementItemDto> RevenueItems { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public List<IncomeStatementItemDto> ExpenseItems { get; set; } = new();
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
    }

    public class IncomeStatementItemDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal Amount { get; set; }
    }

    public class BalanceSheetDto : ReportBaseDto
    {
        public List<BalanceSheetItemDto> Assets { get; set; } = new();
        public decimal TotalAssets { get; set; }
        public List<BalanceSheetItemDto> Liabilities { get; set; } = new();
        public decimal TotalLiabilities { get; set; }
        public List<BalanceSheetItemDto> Equity { get; set; } = new();
        public decimal TotalEquity { get; set; }
        public bool IsBalanced { get; set; }
    }

    public class BalanceSheetItemDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal Balance { get; set; }
    }

    public class CashFlowReportDto : ReportBaseDto
    {
        public List<CashFlowSectionDto> Sections { get; set; } = new();
        public decimal NetCashChange { get; set; }
        public decimal StartingCash { get; set; }
        public decimal EndingCash { get; set; }
    }

    public class CashFlowSectionDto
    {
        public string SectionName { get; set; } // Operating, Investing, Financing
        public List<CashFlowItemDto> Items { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class CashFlowItemDto
    {
        public string Name { get; set; }
        public decimal Amount { get; set; }
    }

    #endregion

    #region Aging Reports

    public class AgingReportDto : ReportBaseDto
    {
        public List<AgingItemDto> Items { get; set; } = new();
        public decimal Total0To30 { get; set; }
        public decimal Total31To60 { get; set; }
        public decimal Total61To90 { get; set; }
        public decimal Total91Plus { get; set; }
        public decimal GrandTotal { get; set; }
    }

    public class AgingItemDto
    {
        public Guid EntityId { get; set; } // Customer or Vendor Id
        public string EntityName { get; set; }
        public decimal Balance0To30 { get; set; }
        public decimal Balance31To60 { get; set; }
        public decimal Balance61To90 { get; set; }
        public decimal Balance91Plus { get; set; }
        public decimal TotalBalance { get; set; }
    }

    #endregion

    #region Sales & Statements

    public class SalesReportDto : ReportBaseDto
    {
        public List<SalesSummaryItemDto> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetSales { get; set; }
    }

    public class SalesSummaryItemDto
    {
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalRemaining { get; set; }
    }

    public class StatementReportDto : ReportBaseDto
    {
        public Guid EntityId { get; set; }
        public string EntityName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<StatementEntryDto> Entries { get; set; } = new();
    }

    public class StatementEntryDto
    {
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public string ReferenceNumber { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }

    #endregion

    #region Project Reports

    public class ProjectProfitabilityDto : ReportBaseDto
    {
        public List<ProjectProfitabilityItemDto> Projects { get; set; } = new();
    }

    public class ProjectProfitabilityItemDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ClientName { get; set; }
        public decimal ContractValue { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal GrossProfit => TotalRevenue - TotalExpenses;
        public decimal ProfitMargin => TotalRevenue != 0 ? (GrossProfit / TotalRevenue) * 100 : 0;
    }

    public class ProjectCostBreakdownDto : ReportBaseDto
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public List<ProjectCostCategoryDto> Categories { get; set; } = new();
        public decimal TotalCost { get; set; }
    }

    public class ProjectCostCategoryDto
    {
        public string CategoryName { get; set; } // e.g., Materials, Labor, Subcontractors
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }
    #endregion
    #region Movement Report

    public class MovementReportDto : ReportBaseDto
    {
        public string Mode { get; set; } // "SingleAccount" or "AllCashAndBank"
        public List<AccountMovementDto> Accounts { get; set; } = new();

        // Helper properties for single account mode
        public string? AccountName { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? TotalIncome { get; set; }
        public decimal? TotalExpense { get; set; }
        public decimal? ClosingBalance { get; set; }
        public List<MovementEntryDto> Movements { get; set; } = new();
    }

    public class AccountMovementDto
    {
        public Guid AccountId { get; set; }
        public string AccountName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<MovementEntryDto> Movements { get; set; } = new();
    }

    public class MovementEntryDto
    {
        public Guid JournalEntryId { get; set; }
        public string EntryNumber { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public string Reference { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal RunningBalance { get; set; }
        public string? ProjectName { get; set; }
        public string? BranchName { get; set; }
    }

    #endregion
}
