using System;
using System.Threading.Tasks;
using fatortak.Services.FinancialReportService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using fatortak.Helpers;
using fatortak.Dtos.Reports;
using fatortak.Dtos.Shared;

namespace fatortak.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FinancialReportController : ControllerBase
    {
        private readonly IFinancialReportService _reportService;

        public FinancialReportController(IFinancialReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("trial-balance")]
        public async Task<ActionResult<ServiceResult<TrialBalanceDto>>> GetTrialBalance([FromQuery] DateTime? asOfDate)
        {
            var result = await _reportService.GetTrialBalanceAsync(asOfDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("ledger/{accountId}")]
        public async Task<ActionResult<ServiceResult<LedgerDto>>> GetLedger(Guid accountId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var result = await _reportService.GetAccountLedgerAsync(accountId, fromDate, toDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("income-statement")]
        public async Task<ActionResult<ServiceResult<IncomeStatementDto>>> GetIncomeStatement([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var result = await _reportService.GetIncomeStatementAsync(fromDate, toDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("balance-sheet")]
        public async Task<ActionResult<ServiceResult<BalanceSheetDto>>> GetBalanceSheet([FromQuery] DateTime asOfDate)
        {
            var result = await _reportService.GetBalanceSheetAsync(asOfDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("cash-flow")]
        public async Task<ActionResult<ServiceResult<CashFlowReportDto>>> GetCashFlow([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var result = await _reportService.GetCashFlowReportAsync(fromDate, toDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("ar-aging")]
        public async Task<ActionResult<ServiceResult<AgingReportDto>>> GetARAging([FromQuery] DateTime asOfDate)
        {
            var result = await _reportService.GetARAgingReportAsync(asOfDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("ap-aging")]
        public async Task<ActionResult<ServiceResult<AgingReportDto>>> GetAPAging([FromQuery] DateTime asOfDate)
        {
            var result = await _reportService.GetAPAgingReportAsync(asOfDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("customer-statement/{customerId}")]
        public async Task<ActionResult<ServiceResult<StatementReportDto>>> GetCustomerStatement(Guid customerId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var result = await _reportService.GetCustomerStatementAsync(customerId, fromDate, toDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("vendor-statement/{vendorId}")]
        public async Task<ActionResult<ServiceResult<StatementReportDto>>> GetVendorStatement(Guid vendorId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var result = await _reportService.GetVendorStatementAsync(vendorId, fromDate, toDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("sales")]
        public async Task<ActionResult<ServiceResult<SalesReportDto>>> GetSales([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] Guid? customerId, [FromQuery] Guid? projectId)
        {
            var result = await _reportService.GetSalesReportAsync(fromDate, toDate, customerId, projectId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("project-profitability")]
        public async Task<ActionResult<ServiceResult<ProjectProfitabilityDto>>> GetProjectProfitability([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var result = await _reportService.GetProjectProfitabilityReportAsync(fromDate, toDate);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("project-cost-breakdown/{projectId}")]
        public async Task<ActionResult<ServiceResult<ProjectCostBreakdownDto>>> GetProjectCostBreakdown(Guid projectId)
        {
            var result = await _reportService.GetProjectCostBreakdownAsync(projectId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("movements")]
        public async Task<ActionResult<ServiceResult<MovementReportDto>>> GetMovements([FromQuery] Guid? accountId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] Guid? projectId, [FromQuery] Guid? branchId)
        {
            var result = await _reportService.GetMovementReportAsync(accountId, fromDate, toDate, projectId, branchId);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
