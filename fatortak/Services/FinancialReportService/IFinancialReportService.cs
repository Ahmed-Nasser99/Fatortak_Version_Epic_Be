using System;
using System.Threading.Tasks;
using fatortak.Dtos.Reports;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Helpers;

namespace fatortak.Services.FinancialReportService
{
    public interface IFinancialReportService
    {
        // Financial Reports
        Task<ServiceResult<TrialBalanceDto>> GetTrialBalanceAsync(DateTime? asOfDate = null);
        Task<ServiceResult<LedgerDto>> GetAccountLedgerAsync(Guid accountId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<ServiceResult<IncomeStatementDto>> GetIncomeStatementAsync(DateTime fromDate, DateTime toDate);
        Task<ServiceResult<BalanceSheetDto>> GetBalanceSheetAsync(DateTime asOfDate);
        Task<ServiceResult<CashFlowReportDto>> GetCashFlowReportAsync(DateTime fromDate, DateTime toDate);

        // Sales & Aging Reports
        Task<ServiceResult<AgingReportDto>> GetARAgingReportAsync(DateTime asOfDate);
        Task<ServiceResult<AgingReportDto>> GetAPAgingReportAsync(DateTime asOfDate);
        Task<ServiceResult<StatementReportDto>> GetCustomerStatementAsync(Guid customerId, DateTime fromDate, DateTime toDate);
        Task<ServiceResult<StatementReportDto>> GetVendorStatementAsync(Guid vendorId, DateTime fromDate, DateTime toDate);
        Task<ServiceResult<SalesReportDto>> GetSalesReportAsync(DateTime fromDate, DateTime toDate, Guid? customerId = null, Guid? projectId = null);

        // Project Reports
        Task<ServiceResult<ProjectProfitabilityDto>> GetProjectProfitabilityReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<ServiceResult<ProjectCostBreakdownDto>> GetProjectCostBreakdownAsync(Guid projectId);

        // Movement Report
        Task<ServiceResult<MovementReportDto>> GetMovementReportAsync(Guid? accountId, DateTime fromDate, DateTime toDate, Guid? projectId = null, Guid? branchId = null);
    }
}
