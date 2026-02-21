using fatortak.Dtos.Dashboard;
using fatortak.Dtos.Invoice;
using fatortak.Dtos.Report;
using fatortak.Dtos.Report.Stock;
using fatortak.Dtos.Shared;

namespace fatortak.Services.ReportsService
{
    public interface IReportsService
    {
        Task<ServiceResult<ReportStatsDto>> GetReportStatsAsync(string period, Guid? projectId = null);
        Task<ServiceResult<List<RevenueDataPointDto>>> GetRevenueDataAsync(string period, Guid? projectId = null);
        Task<ServiceResult<List<TopCustomerDto>>> GetTopCustomersAsync(string period, int topCount = 5, Guid? projectId = null);
        Task<ServiceResult<List<TopSupplierDto>>> GetTopSuppliersAsync(string period, int topCount = 5, Guid? projectId = null);
        Task<ServiceResult<CashFlowDto>> GetCashFlowAsync(string period, Guid? projectId = null);
        Task<ServiceResult<ProfitAnalysisDto>> GetProfitAnalysisAsync(string period, Guid? projectId = null);
        Task<ServiceResult<PagedResponseDto<InvoiceDto>>> GetSalesReport(
        InvoiceFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<PagedResponseDto<TransactionDto>>> GetExpensesReport(
        InvoiceFilterDto filter, PaginationDto pagination, string? expensesStatus);
        Task<ServiceResult<AccountStatementDto>> GetAccountStatementAsync(AccountStatementFilterDto filter);
        Task<ServiceResult<PagedResponseDto<TransactionDto>>> GetRecentTransactionsAsync(
            InvoiceFilterDto filter,
            PaginationDto pagination,
            string? type);

        Task<ServiceResult<PagedResponseDto<CurrentStockReportDto>>> GetCurrentStockReportAsync(
        StockReportFilterDto filter, PaginationDto pagination);

        Task<ServiceResult<List<ItemMovementReportDto>>> GetItemMovementReportAsync(
            ItemMovementFilterDto filter);

        Task<ServiceResult<PagedResponseDto<ItemProfitabilityReportDto>>> GetItemProfitabilityReportAsync(
            ItemProfitabilityFilterDto filter, PaginationDto pagination);

        Task<ServiceResult<ProjectSheetDto>> GetProjectSheetAsync(Guid projectId, DateTime? fromDate, DateTime? toDate);
        Task<ServiceResult<TreasuryReportDto>> GetTreasuryReportAsync(DateTime? fromDate, DateTime? toDate);
        Task<ServiceResult<AccountStatementDto>> GetSupplierLedgerAsync(Guid supplierId, DateTime? fromDate, DateTime? toDate);
    }
}
