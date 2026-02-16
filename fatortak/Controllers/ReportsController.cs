using fatortak.Dtos.Invoice;
using fatortak.Dtos.Report;
using fatortak.Dtos.Report.Stock;
using fatortak.Dtos.Shared;
using fatortak.Dtos.Dashboard;
using fatortak.Services.ReportsService;
using fatortak.Services.TransactionService;
using fatortak.Services.ReportService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using fatortak.Services.ItemService;
using fatortak.Dtos.Transaction;

namespace fatortak.Controllers
{
    [Authorize]
    [Route("api/reports")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly IReportsService _reportsService;
        private readonly ITransactionService _transactionService;
        private readonly IReportExportService _reportExportService;
        private readonly IItemService _itemService;

        public ReportsController(
            IReportsService reportsService,
            ITransactionService transactionService,
            IReportExportService reportExportService,
            IItemService itemService)
        {
            _reportsService = reportsService;
            _transactionService = transactionService;
            _reportExportService = reportExportService;
            _itemService = itemService;
        }

        [HttpGet("transactions/export")]
        public async Task<IActionResult> ExportTransactions(
            [FromQuery] InvoiceFilterDto filter,
            [FromQuery] string? type,
            [FromQuery] string format = "excel",
            [FromQuery] string lang = "en")
        {
            var pagination = new PaginationDto { PageNumber = 1, PageSize = 100000 };
            // Use GetRecentTransactionsAsync to match frontend data source
            var result = await _reportsService.GetRecentTransactionsAsync(filter, pagination, type);
            if (!result.Success) return BadRequest(result.ErrorMessage);

            var transactions = result.Data.Data.ToList();

            var metadata = new ReportMetadata
            {
                Title = lang == "ar" ? "تقرير المعاملات" : "Transactions Report",
                UserEmail = User.Identity?.Name ?? "Unknown",
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Language = lang,
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = lang == "ar" ? "التاريخ" : "Date", PropertyName = "Date" },
                    new ReportColumn { Header = lang == "ar" ? "النوع" : "Type", PropertyName = "Type" },
                    new ReportColumn { Header = lang == "ar" ? "المرجع" : "Reference", PropertyName = "Reference" },
                    new ReportColumn { Header = lang == "ar" ? "المبلغ" : "Amount", PropertyName = "Amount", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "الحالة" : "Status", PropertyName = "Status" }
                }
            };

            if (filter.FromDate.HasValue) metadata.Filters.Add(lang == "ar" ? "من" : "From", filter.FromDate.Value.ToString("yyyy-MM-dd"));
            if (filter.ToDate.HasValue) metadata.Filters.Add(lang == "ar" ? "إلى" : "To", filter.ToDate.Value.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrEmpty(type)) metadata.Filters.Add(lang == "ar" ? "النوع" : "Type", type);

            return await GenerateExportFile(transactions, metadata, format, "Transactions_Report");
        }

        [HttpGet("sales/export")]
        public async Task<IActionResult> ExportSales(
            [FromQuery] InvoiceFilterDto filter,
            [FromQuery] string format = "excel",
            [FromQuery] string lang = "en")
        {
            var pagination = new PaginationDto { PageNumber = 1, PageSize = 100000 };
            var result = await _reportsService.GetSalesReport(filter, pagination);
            var data = result.Data.Data.ToList();

            var metadata = new ReportMetadata
            {
                Title = lang == "ar" ? "تقرير المبيعات" : "Sales Report",
                UserEmail = User.Identity?.Name ?? "Unknown",
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Language = lang,
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = lang == "ar" ? "رقم الفاتورة" : "Invoice #", PropertyName = "InvoiceNumber" },
                    new ReportColumn { Header = lang == "ar" ? "التاريخ" : "Date", PropertyName = "IssueDate", Format = "d" },
                    new ReportColumn { Header = lang == "ar" ? "العميل" : "Customer", PropertyName = "CustomerName" },
                    new ReportColumn { Header = lang == "ar" ? "الإجمالي" : "Total", PropertyName = "Total", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "المدفوع" : "Paid", PropertyName = "AmountPaid", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "المتبقي" : "Remaining", PropertyName = "RemainingAmount", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "الحالة" : "Status", PropertyName = "Status" },
                    new ReportColumn { Header = lang == "ar" ? "تاريخ الاستحقاق" : "Due Date", PropertyName = "DueDate", Format = "d" }
                }
            };

            return await GenerateExportFile<InvoiceDto>(data, metadata, format, "Sales_Report");
        }

        [HttpGet("expenses/export")]
        public async Task<IActionResult> ExportExpenses(
            [FromQuery] InvoiceFilterDto filter,
            [FromQuery] string? expensesStatus,
            [FromQuery] string format = "excel",
            [FromQuery] string lang = "en")
        {
            var pagination = new PaginationDto { PageNumber = 1, PageSize = 100000 };
            var result = await _reportsService.GetExpensesReport(filter, pagination, expensesStatus);
            var data = result.Data.Data.ToList();

            var metadata = new ReportMetadata
            {
                Title = lang == "ar" ? "تقرير المصروفات" : "Expenses Report",
                UserEmail = User.Identity?.Name ?? "Unknown",
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Language = lang,
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = lang == "ar" ? "التاريخ" : "Date", PropertyName = "Date", Format = "d" },
                    new ReportColumn { Header = lang == "ar" ? "النوع" : "Type", PropertyName = "Type" },
                    new ReportColumn { Header = lang == "ar" ? "المرجع" : "Reference", PropertyName = "Reference" },
                    new ReportColumn { Header = lang == "ar" ? "المبلغ" : "Amount", PropertyName = "Amount", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "المدفوع" : "Paid", PropertyName = "Paid", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "المتبقي" : "Remaining", PropertyName = "Remaining", Format = "C" }
                }
            };

            return await GenerateExportFile<fatortak.Dtos.Dashboard.TransactionDto>(data, metadata, format, "Expenses_Report");
        }

        [HttpGet("stock/current/export")]
        public async Task<IActionResult> ExportCurrentStock(
            [FromQuery] StockReportFilterDto filter,
            [FromQuery] string format = "excel",
            [FromQuery] string lang = "en")
        {
            var pagination = new PaginationDto { PageNumber = 1, PageSize = 100000 };
            var result = await _reportsService.GetCurrentStockReportAsync(filter, pagination);
            var data = result.Data.Data.ToList();

            var metadata = new ReportMetadata
            {
                Title = lang == "ar" ? "تقرير المخزون الحالي" : "Current Stock Report",
                UserEmail = User.Identity?.Name ?? "Unknown",
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Language = lang,
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = lang == "ar" ? "الكود" : "Code", PropertyName = "ItemCode" },
                    new ReportColumn { Header = lang == "ar" ? "الاسم" : "Name", PropertyName = "ItemName" },
                    new ReportColumn { Header = lang == "ar" ? "المباع" : "Sold Qty", PropertyName = "SoldQty" },
                    new ReportColumn { Header = lang == "ar" ? "المخزون" : "In Stock", PropertyName = "InStock" },
                    new ReportColumn { Header = lang == "ar" ? "سعر الشراء" : "Purchase Price", PropertyName = "PurchasePrice", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "سعر البيع" : "Sell Price", PropertyName = "SellPrice", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "القيمة الإجمالية" : "Total Value", PropertyName = "TotalValue", Format = "C" }
                }
            };

            return await GenerateExportFile<CurrentStockReportDto>(data, metadata, format, "Stock_Report");
        }

        [HttpGet("account-statement/export")]
        public async Task<IActionResult> ExportAccountStatement(
            [FromQuery] Guid customerId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string invoiceType = "Sell",
            [FromQuery] string format = "excel",
            [FromQuery] string lang = "en")
        {
            var start = startDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var end = endDate ?? DateTime.UtcNow;

            var filter = new AccountStatementFilterDto
            {
                CustomerId = customerId,
                StartDate = start,
                EndDate = end,
                InvoiceType = invoiceType
            };

            var result = await _reportsService.GetAccountStatementAsync(filter);
            if (!result.Success) return BadRequest(result.ErrorMessage);
            
            var data = result.Data.Transactions;

            var metadata = new ReportMetadata
            {
                Title = lang == "ar" ? "كشف حساب" : "Account Statement",
                UserEmail = User.Identity?.Name ?? "Unknown",
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Language = lang,
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = lang == "ar" ? "التاريخ" : "Date", PropertyName = "Date", Format = "d" },
                    new ReportColumn { Header = lang == "ar" ? "نوع الحركة" : "Transaction Type", PropertyName = "TransactionType" },
                    new ReportColumn { Header = lang == "ar" ? "التفاصيل" : "Details", PropertyName = "TransactionDetails" },
                    new ReportColumn { Header = lang == "ar" ? "قيمة الفاتورة" : "Invoice Amount", PropertyName = "InvoiceAmount", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "المدفوع" : "Payment", PropertyName = "PaymentAmount", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "الرصيد" : "Balance", PropertyName = "Balance", Format = "C" }
                }
            };

            if (result.Data.CustomerInfo != null)
            {
                metadata.Filters.Add(lang == "ar" ? "العميل" : "Client", result.Data.CustomerInfo.Name);
            }

            return await GenerateExportFile(data, metadata, format, "Account_Statement");
        }

        [HttpGet("item-movement/export")]
        public async Task<IActionResult> ExportItemMovement(
            [FromQuery] Guid itemId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string format = "excel",
            [FromQuery] string lang = "en")
        {
            var filter = new ItemMovementFilterDto
            {
                ItemId = itemId,
                FromDate = fromDate,
                ToDate = toDate
            };

            var result = await _reportsService.GetItemMovementReportAsync(filter);
            if (!result.Success) return BadRequest(result.ErrorMessage);

            var data = result.Data;

            var metadata = new ReportMetadata
            {
                Title = lang == "ar" ? "تقرير حركة الصنف" : "Item Movement Report",
                UserEmail = User.Identity?.Name ?? "Unknown",
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Language = lang,
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = lang == "ar" ? "التاريخ" : "Date", PropertyName = "Date", Format = "d" },
                    new ReportColumn { Header = lang == "ar" ? "رقم الفاتورة" : "Invoice #", PropertyName = "InvoiceNumber" },
                    new ReportColumn { Header = lang == "ar" ? "النوع" : "Type", PropertyName = "Type" },
                    new ReportColumn { Header = lang == "ar" ? "وارد" : "In", PropertyName = "QtyIn" },
                    new ReportColumn { Header = lang == "ar" ? "صادر" : "Out", PropertyName = "QtyOut" },
                    new ReportColumn { Header = lang == "ar" ? "الرصيد" : "Balance", PropertyName = "Balance" },
                    new ReportColumn { Header = lang == "ar" ? "سعر الوحدة" : "Unit Price", PropertyName = "UnitPrice", Format = "C" }
                }
            };

            var itemResult = await _itemService.GetItemAsync(itemId);
            if (itemResult.Success)
            {
                metadata.Filters.Add(lang == "ar" ? "الصنف" : "Item", itemResult.Data.Name);
            }

            return await GenerateExportFile(data, metadata, format, "Item_Movement_Report");
        }

        [HttpGet("item-profitability/export")]
        public async Task<IActionResult> ExportItemProfitability(
            [FromQuery] ItemProfitabilityFilterDto filter,
            [FromQuery] string format = "excel",
            [FromQuery] string lang = "en")
        {
            var pagination = new PaginationDto { PageNumber = 1, PageSize = 100000 };
            var result = await _reportsService.GetItemProfitabilityReportAsync(filter, pagination);
            var data = result.Data.Data.ToList();

            var metadata = new ReportMetadata
            {
                Title = lang == "ar" ? "تقرير ربحية الأصناف" : "Item Profitability Report",
                UserEmail = User.Identity?.Name ?? "Unknown",
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Language = lang,
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = lang == "ar" ? "الكود" : "Code", PropertyName = "ItemCode" },
                    new ReportColumn { Header = lang == "ar" ? "الاسم" : "Name", PropertyName = "ItemName" },
                    new ReportColumn { Header = lang == "ar" ? "إجمالي المبيعات" : "Total Sales", PropertyName = "TotalSales", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "إجمالي التكلفة" : "Total Cost", PropertyName = "TotalCost", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "الربح" : "Profit", PropertyName = "Profit", Format = "C" },
                    new ReportColumn { Header = lang == "ar" ? "نسبة الربح" : "Profit %", PropertyName = "ProfitPercentage", Format = "0.0" }
                }
            };

            return await GenerateExportFile<ItemProfitabilityReportDto>(data, metadata, format, "Item_Profitability_Report");
        }

        private async Task<IActionResult> GenerateExportFile<T>(List<T> data, ReportMetadata metadata, string format, string baseFileName)
        {
            byte[] fileContent;
            string contentType;
            string fileName;

            if (format.ToLower() == "pdf")
            {
                fileContent = await _reportExportService.ExportToPdfAsync(data, metadata);
                contentType = "application/pdf";
                fileName = $"{baseFileName}_{DateTime.Now:yyyyMMddHHmm}.pdf";
            }
            else
            {
                fileContent = await _reportExportService.ExportToExcelAsync(data, metadata);
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                fileName = $"{baseFileName}_{DateTime.Now:yyyyMMddHHmm}.xlsx";
            }

            return File(fileContent, contentType, fileName);
        }

        [HttpGet("stats")]
        public async Task<ActionResult<ServiceResult<ReportStatsDto>>> GetStats([FromQuery] string period = "month")
        {
            return Ok(await _reportsService.GetReportStatsAsync(period));
        }

        [HttpGet("revenue")]
        public async Task<ActionResult<ServiceResult<List<RevenueDataPointDto>>>> GetRevenue([FromQuery] string period = "month")
        {
            return Ok(await _reportsService.GetRevenueDataAsync(period));
        }

        [HttpGet("top-customers")]
        public async Task<ActionResult<ServiceResult<List<TopCustomerDto>>>> GetTopCustomers(
            [FromQuery] string period = "month",
            [FromQuery] int top = 5)
        {
            return Ok(await _reportsService.GetTopCustomersAsync(period, top));
        }

        [HttpGet("top-suppliers")]
        public async Task<ActionResult<ServiceResult<List<TopSupplierDto>>>> GetTopSuppliers(
            [FromQuery] string period = "month",
            [FromQuery] int top = 5)
        {
            return Ok(await _reportsService.GetTopSuppliersAsync(period, top));
        }

        [HttpGet("cashflow")]
        public async Task<ActionResult<ServiceResult<CashFlowDto>>> GetCashFlow([FromQuery] string period = "month")
        {
            return Ok(await _reportsService.GetCashFlowAsync(period));
        }

        [HttpGet("profit")]
        public async Task<ActionResult<ServiceResult<ProfitAnalysisDto>>> GetProfitAnalysis([FromQuery] string period = "month")
        {
            return Ok(await _reportsService.GetProfitAnalysisAsync(period));
        }

        [HttpGet("SalesInvoices")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<InvoiceDto>>>> GetSalesInvoices([FromQuery] InvoiceFilterDto filter, [FromQuery] PaginationDto pagination)
        {
            return Ok(await _reportsService.GetSalesReport(filter, pagination));
        }

        [HttpGet("Expenses")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<InvoiceDto>>>> GetExpensesReport([FromQuery] InvoiceFilterDto filter, [FromQuery] PaginationDto pagination , [FromQuery] string? expensesStatus)
        {
            return Ok(await _reportsService.GetExpensesReport(filter, pagination, expensesStatus));
        }

        [HttpGet("Transactions")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<InvoiceDto>>>> GetTransactionReport([FromQuery] InvoiceFilterDto filter, [FromQuery] PaginationDto pagination, [FromQuery] string? type)
        {
            return Ok(await _reportsService.GetRecentTransactionsAsync(filter, pagination, type));
        }
        [HttpGet("account-statement")]
        [Authorize]
        public async Task<IActionResult> GetAccountStatement(
            [FromQuery] Guid customerId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string invoiceType = "Sell")
        {
            if (customerId == Guid.Empty)
            {
                return BadRequest(new { message = "Customer ID is required" });
            }

            // Default to current month if dates not provided
            var start = startDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var end = endDate ?? DateTime.UtcNow;

            var filter = new AccountStatementFilterDto
            {
                CustomerId = customerId,
                StartDate = start,
                EndDate = end,
                InvoiceType = invoiceType
            };

            var result = await _reportsService.GetAccountStatementAsync(filter);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(result.Data);
        }


        [HttpGet("current-stock")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<CurrentStockReportDto>>>> GetCurrentStock(
            [FromQuery] StockReportFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            var result = await _reportsService.GetCurrentStockReportAsync(filter, pagination);
            return Ok(result);
        }

        [HttpGet("item-movement")]
        public async Task<ActionResult<ServiceResult<List<ItemMovementReportDto>>>> GetItemMovement(
            [FromQuery] Guid itemId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            if (itemId == Guid.Empty)
            {
                return BadRequest(new { message = "Item ID is required" });
            }

            var filter = new ItemMovementFilterDto
            {
                ItemId = itemId,
                FromDate = fromDate,
                ToDate = toDate
            };

            var result = await _reportsService.GetItemMovementReportAsync(filter);

            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            return Ok(result);
        }

        [HttpGet("item-profitability")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<ItemProfitabilityReportDto>>>> GetItemProfitability(
            [FromQuery] ItemProfitabilityFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            var result = await _reportsService.GetItemProfitabilityReportAsync(filter, pagination);
            return Ok(result);
        }

        [HttpGet("low-stock")]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<CurrentStockReportDto>>>> GetLowStock(
            [FromQuery] PaginationDto pagination)
        {
            var filter = new StockReportFilterDto { LowStock = true };
            var result = await _reportsService.GetCurrentStockReportAsync(filter, pagination);
            return Ok(result);
        }

        [HttpGet("project-sheet")]
        public async Task<ActionResult<ServiceResult<ProjectSheetDto>>> GetProjectSheet(
            [FromQuery] Guid projectId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            if (projectId == Guid.Empty) return BadRequest(new { message = "Project ID is required" });
            return Ok(await _reportsService.GetProjectSheetAsync(projectId, fromDate, toDate));
        }

        [HttpGet("treasury")]
        public async Task<ActionResult<ServiceResult<TreasuryReportDto>>> GetTreasuryReport(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            return Ok(await _reportsService.GetTreasuryReportAsync(fromDate, toDate));
        }

        [HttpGet("supplier-ledger")]
        public async Task<ActionResult<ServiceResult<AccountStatementDto>>> GetSupplierLedger(
            [FromQuery] Guid supplierId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            if (supplierId == Guid.Empty) return BadRequest(new { message = "Supplier ID is required" });
            return Ok(await _reportsService.GetSupplierLedgerAsync(supplierId, fromDate, toDate));
        }
    }
}