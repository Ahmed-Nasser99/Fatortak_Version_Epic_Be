using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Company;
using fatortak.Dtos.Dashboard;
using fatortak.Dtos.Invoice;
using fatortak.Dtos.Report;
using fatortak.Dtos.Report.Stock;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace fatortak.Services.ReportsService
{
    public class ReportsService : IReportsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportsService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ReportsService(ApplicationDbContext context, ILogger<ReportsService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        #region Report Stats
        public async Task<ServiceResult<ReportStatsDto>> GetReportStatsAsync(string period = "month")
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // ✅ Include Paid + PartialPaid revenues
                var revenue = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .SumAsync(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue
                        ? i.AmountPaid.Value
                        : i.Total);

                // ✅ Expenses (Paid + PartialPaid)
                var expenses = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .SumAsync(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue
                        ? i.AmountPaid.Value
                        : i.Total);

                // ✅ Salaries + Other Expenses
                var totalSalaries = await _context.Employees
                    .Where(e => e.TenantId == _tenantId)
                    .SumAsync(i => i.Salary);

                var otherExpenses = await _context.Expenses
                    .Where(e => e.TenantId == _tenantId &&
                                e.Date >= DateOnly.FromDateTime(startDate) &&
                                e.Date <= DateOnly.FromDateTime(endDate))
                    .SumAsync(e => e.Total);

                var invoices = await _context.Invoices
                    .CountAsync(i => i.TenantId == _tenantId &&
                                     i.IssueDate >= startDate &&
                                     i.IssueDate <= endDate);

                var customers = await _context.Customers
                    .CountAsync(c => c.TenantId == _tenantId && !c.IsSupplier && c.IsActive && !c.IsDeleted);

                var suppliers = await _context.Customers
                    .CountAsync(c => c.TenantId == _tenantId && c.IsSupplier && c.IsActive && !c.IsDeleted);

                return ServiceResult<ReportStatsDto>.SuccessResult(new ReportStatsDto
                {
                    TotalRevenue = revenue,
                    TotalExpenses = expenses + otherExpenses + totalSalaries.GetValueOrDefault(),
                    NetIncome = revenue - (expenses + otherExpenses + totalSalaries.GetValueOrDefault()),
                    TotalInvoices = invoices,
                    ActiveCustomers = customers,
                    ActiveSuppliers = suppliers,
                    TotalSalaries = totalSalaries
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report stats");
                return ServiceResult<ReportStatsDto>.Failure("Failed to retrieve stats");
            }
        }
        #endregion

        #region Revenue Data
        public async Task<ServiceResult<List<RevenueDataPointDto>>> GetRevenueDataAsync(string period = "month")
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                var query = _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()));

                List<RevenueDataPointDto> data;

                if (period == "month")
                {
                    data = await query
                        .GroupBy(i => i.IssueDate.Date)
                        .Select(g => new RevenueDataPointDto
                        {
                            Period = g.Key.ToString("dd MMM"),
                            Revenue = g.Sum(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total),
                            Orders = g.Count()
                        })
                        .ToListAsync();
                }
                else
                {
                    data = await query
                        .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
                        .Select(g => new RevenueDataPointDto
                        {
                            Period = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month),
                            Revenue = g.Sum(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total),
                            Orders = g.Count()
                        })
                        .ToListAsync();
                }

                return ServiceResult<List<RevenueDataPointDto>>.SuccessResult(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting revenue data");
                return ServiceResult<List<RevenueDataPointDto>>.Failure("Failed to retrieve revenue data");
            }
        }
        #endregion

        #region Top Customers
        public async Task<ServiceResult<List<TopCustomerDto>>> GetTopCustomersAsync(string period = "month", int topCount = 5)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                var result = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .GroupBy(i => new { i.CustomerId, i.Customer.Name })
                    .Select(g => new TopCustomerDto
                    {
                        Id = g.Key.CustomerId,
                        Name = g.Key.Name,
                        Orders = g.Count(),
                        TotalSpent = g.Sum(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total),
                        LastOrderDate = g.Max(i => i.IssueDate),
                        Status = g.Sum(i => i.Total) > 10000 ? "VIP" : "Regular"
                    })
                    .OrderByDescending(c => c.TotalSpent)
                    .Take(topCount)
                    .ToListAsync();

                return ServiceResult<List<TopCustomerDto>>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top customers");
                return ServiceResult<List<TopCustomerDto>>.Failure("Failed to retrieve top customers");
            }
        }
        #endregion

        #region Top Suppliers
        public async Task<ServiceResult<List<TopSupplierDto>>> GetTopSuppliersAsync(string period = "month", int topCount = 5)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                var result = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .GroupBy(i => new { i.CustomerId, i.Customer.Name })
                    .Select(g => new TopSupplierDto
                    {
                        Id = g.Key.CustomerId,
                        Name = g.Key.Name,
                        Orders = g.Count(),
                        TotalAmount = g.Sum(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total),
                        LastOrderDate = g.Max(i => i.IssueDate)
                    })
                    .OrderByDescending(c => c.TotalAmount)
                    .Take(topCount)
                    .ToListAsync();

                return ServiceResult<List<TopSupplierDto>>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top suppliers");
                return ServiceResult<List<TopSupplierDto>>.Failure("Failed to retrieve top suppliers");
            }
        }
        #endregion

        #region Cash Flow
        public async Task<ServiceResult<CashFlowDto>> GetCashFlowAsync(string period = "month")
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                var cashIn = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .SumAsync(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total);

                var cashOut = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .SumAsync(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total);

                var otherExpenses = await _context.Expenses
                    .Where(i => i.TenantId == _tenantId &&
                                i.Date >= DateOnly.FromDateTime(startDate) &&
                                i.Date <= DateOnly.FromDateTime(endDate))
                    .SumAsync(i => i.Total);

                var totalSalaries = await _context.Employees
                   .Where(e => e.TenantId == _tenantId)
                   .SumAsync(i => i.Salary);

                var outstanding = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.DueDate < DateTime.UtcNow &&
                                i.Status != InvoiceStatus.Paid.ToString())
                    .SumAsync(i => i.Total - (i.AmountPaid ?? 0));

                return ServiceResult<CashFlowDto>.SuccessResult(new CashFlowDto
                {
                    CashIn = cashIn,
                    CashOut = cashOut + otherExpenses + totalSalaries.GetValueOrDefault(),
                    NetCashFlow = cashIn - (cashOut + otherExpenses + totalSalaries.GetValueOrDefault()),
                    TotalExpenses = otherExpenses,
                    TotalPurchaseInvoices = cashOut,
                    TotalSalaries = totalSalaries,
                    OutstandingReceivables = outstanding
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cash flow");
                return ServiceResult<CashFlowDto>.Failure("Failed to retrieve cash flow");
            }
        }
        #endregion

        #region Profit Analysis
        public async Task<ServiceResult<ProfitAnalysisDto>> GetProfitAnalysisAsync(string period = "month")
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                var revenue = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .SumAsync(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total);

                var expenses = await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                i.IssueDate >= startDate && i.IssueDate <= endDate &&
                                (i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()))
                    .SumAsync(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue ? i.AmountPaid.Value : i.Total);

                var otherExpenses = await _context.Expenses
                    .Where(i => i.TenantId == _tenantId &&
                                i.Date >= DateOnly.FromDateTime(startDate) &&
                                i.Date <= DateOnly.FromDateTime(endDate))
                    .SumAsync(i => i.Total);

                var totalSalaries = await _context.Employees
                    .Where(e => e.TenantId == _tenantId)
                    .SumAsync(i => i.Salary);

                var grossProfit = revenue - expenses;
                var netProfit = revenue - (expenses + otherExpenses + totalSalaries.GetValueOrDefault());
                var grossMargin = revenue > 0 ? (grossProfit / revenue) * 100 : 0;

                return ServiceResult<ProfitAnalysisDto>.SuccessResult(new ProfitAnalysisDto
                {
                    Revenue = revenue,
                    NetProfit = netProfit,
                    GrossMargin = grossMargin
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profit analysis");
                return ServiceResult<ProfitAnalysisDto>.Failure("Failed to retrieve profit analysis");
            }
        }
        #endregion

        #region Sales Report
        public async Task<ServiceResult<PagedResponseDto<InvoiceDto>>> GetSalesReport(
        InvoiceFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var baseQuery = _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType.ToLower() == InvoiceTypes.Sell.ToString().ToLower() &&
                            i.Status != InvoiceStatus.Cancelled.ToString() &&
                            i.Status != InvoiceStatus.Draft.ToString())
                    .AsQueryable();

                // Apply filters to base query
                var filteredQuery = ApplyInvoicesFilters(baseQuery, filter);

                // Get total count for pagination
                var totalCount = await filteredQuery.CountAsync();

                // Calculate statistics using database aggregation (more efficient)
                var statsQuery = filteredQuery.GroupBy(i => 1).Select(g => new
                {
                    totalSales = g.Sum(i => i.Total),
                    totalPaid = g.Where(i => i.Status == InvoiceStatus.Paid.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString())
                    .Sum(i => i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue
                    ? i.AmountPaid.Value
                    : i.Total),
                    totalReceivables = g.Where(i => i.Status == InvoiceStatus.Pending.ToString() || i.Status == InvoiceStatus.PartialPaid.ToString()).Sum(i => (i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue) ? i.Total - i.AmountPaid.Value : i.Total),
                    totalCount = g.Count(),
                });

                var stats = await statsQuery.FirstOrDefaultAsync() ?? new
                {
                    totalSales = 0m,
                    totalPaid = 0m,
                    totalReceivables = 0m,
                    totalCount = 0,
                };
                var invoices = await filteredQuery
                    .Include(i => i.Customer)
                     .Include(i => i.Installments)
                    .Include(i => i.InvoiceItems)
                    .ThenInclude(ii => ii.Item)
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var invoiceDtos = invoices.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<InvoiceDto>>.SuccessResult(
                    new PagedResponseDto<InvoiceDto>
                    {
                        Data = invoiceDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving invoices with filters: {@Filter}", filter);
                return ServiceResult<PagedResponseDto<InvoiceDto>>.Failure("Failed to retrieve invoices");
            }
        }
        #endregion
        #region Expenses Report
        public async Task<ServiceResult<PagedResponseDto<TransactionDto>>> GetExpensesReport(
            InvoiceFilterDto filter, PaginationDto pagination, string? expensesStatus)
        {
            try
            {
                var transactions = new List<TransactionDto>();
                decimal totalPurchased = 0, totalPaid = 0, totalPayables = 0;

                // Handle all cases: "purchase", "expense", or empty (both)
                bool showPurchases = string.IsNullOrEmpty(expensesStatus) || expensesStatus == "purchase";
                bool showExpenses = string.IsNullOrEmpty(expensesStatus) || expensesStatus == "expense";

                if (showPurchases)
                {
                    // 1️⃣ Base invoice query (Buy invoices only)
                    var invoiceQuery = _context.Invoices
                        .Where(i => i.TenantId == _tenantId)
                        .Where(i => i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                i.Status != InvoiceStatus.Cancelled.ToString() &&
                                i.Status != InvoiceStatus.Draft.ToString())
                        .AsQueryable();

                    // Apply invoice filters
                    invoiceQuery = ApplyInvoicesFilters(invoiceQuery, filter);

                    // 2️⃣ Load FULL invoices (no pagination yet)
                    var invoices = await invoiceQuery
                        .Include(i => i.Customer)
                        .Include(i => i.Installments)
                        .Include(i => i.InvoiceItems).ThenInclude(ii => ii.Item)
                        .OrderByDescending(i => i.CreatedAt)
                        .Select(i => new
                        {
                            Date = i.CreatedAt,
                            Type = "Purchase",
                            Reference = i.InvoiceNumber,
                            Amount = i.Total,
                            TargetId = i.Id.ToString(),
                            Paid = i.Status == InvoiceStatus.Paid.ToString()
                                ? i.Total
                                : (i.AmountPaid ?? 0),
                            PendingAmount =
                            (i.Status == InvoiceStatus.Pending.ToString() ||
                             i.Status == InvoiceStatus.PartialPaid.ToString())
                                ? (i.Status == InvoiceStatus.PartialPaid.ToString() && i.AmountPaid.HasValue
                                    ? i.Total - i.AmountPaid.Value
                                    : i.Total)
                                : 0m,
                            Status = i.Status
                        })
                        .ToListAsync();

                    // 3️⃣ Convert invoices to transactions and calculate stats
                    transactions.AddRange(invoices.Select(i =>
                        new TransactionDto
                        {
                            TransactionDateTime = i.Date,
                            Date = i.Date.ToString("dd MMM yyyy"),
                            Type = i.Type,
                            Reference = i.Reference,
                            TargetId = i.TargetId,
                            Description = "Purchase Invoice",
                            Amount = i.Amount,
                            Paid = i.Paid,
                            Remaining = i.Amount - i.Paid
                        }
                    ));

                    // Only add to totals if we're specifically in purchase mode or showing all
                    if (string.IsNullOrEmpty(expensesStatus) || expensesStatus == "purchase")
                    {
                        totalPurchased += invoices.Sum(i => i.Amount);
                        totalPaid += invoices.Sum(i => i.Paid);
                        totalPayables += invoices.Sum(i => i.PendingAmount);
                    }
                }

                if (showExpenses)
                {
                    // 4️⃣ Load expenses with filters
                    var expenseQuery = _context.Expenses
                        .Where(e => e.TenantId == _tenantId)
                        .AsQueryable();

                    // Apply expense filters
                    expenseQuery = ApplyExpenseFilters(expenseQuery, filter);

                    var expenses = await expenseQuery
                        .OrderByDescending(e => e.CreatedAt)
                        .Select(e => new
                        {
                            Id = e.Id,
                            Date = e.CreatedAt,
                            Amount = e.Total,
                            Notes = e.Notes
                        })
                        .ToListAsync();

                    // Convert expenses to transactions and calculate stats
                    transactions.AddRange(expenses.Select(e => new TransactionDto
                    {
                        TransactionDateTime = e.Date,
                        Date = e.Date.ToString("dd MMM yyyy"),
                        Type = "Expense",
                        TargetId = e.Id.ToString(),
                        Reference = e.Id.ToString(),
                        Description = e.Notes,
                        Amount = e.Amount,
                        Paid = e.Amount,
                        Remaining = 0
                    }));

                    // Only add to totals if we're specifically in expense mode or showing all
                    if (string.IsNullOrEmpty(expensesStatus) || expensesStatus == "expense")
                    {
                        totalPurchased += expenses.Sum(e => e.Amount);
                        totalPaid += expenses.Sum(e => e.Amount);
                        totalPayables += 0; // Expenses are always fully paid
                    }
                }

                // 5️⃣ Sort + paginate
                var totalCount = transactions.Count;

                var paginatedData = transactions
                    .OrderByDescending(t => t.TransactionDateTime)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToList();

                var stats = new
                {
                    totalPurchased,
                    totalPaid,
                    totalPayables,
                    totalCount
                };

                // 7️⃣ Return final response
                return ServiceResult<PagedResponseDto<TransactionDto>>.SuccessResult(
                    new PagedResponseDto<TransactionDto>
                    {
                        Data = paginatedData,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expenses report with filters: {@Filter}", filter);
                return ServiceResult<PagedResponseDto<TransactionDto>>.Failure("Failed to retrieve expenses report");
            }
        }
        #endregion

        #region Expense Filter Helper
        private IQueryable<Expenses> ApplyExpenseFilters(IQueryable<Expenses> query, InvoiceFilterDto filter)
        {
            // Search filter - search in notes
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var searchTerm = filter.Search.ToLower();
                query = query.Where(e =>
                    (e.Notes != null && e.Notes.ToLower().Contains(searchTerm))
                );
            }

            // Date range filters
            if (filter.FromDate.HasValue)
                query = query.Where(e => e.Date >= DateOnly.FromDateTime(filter.FromDate.Value));

            if (filter.ToDate.HasValue)
                query = query.Where(e => e.Date <= DateOnly.FromDateTime(filter.ToDate.Value));

            // Price range filters
            if (filter.minimumPrice.HasValue)
                query = query.Where(e => e.Total >= filter.minimumPrice.Value);

            if (filter.maximumPrice.HasValue)
                query = query.Where(e => e.Total <= filter.maximumPrice.Value);

            return query;
        }
        #endregion
        #region Helpers
        private (DateTime start, DateTime end) GetDateRange(string period)
        {
            var now = DateTime.UtcNow;
            return period switch
            {
                "week" => (now.AddDays(-7), now),
                "month" => (new DateTime(now.Year, now.Month, 1), now),
                "quarter" => (now.AddMonths(-3), now),
                "year" => (new DateTime(now.Year, 1, 1), now),
                _ => (now.AddDays(-7), now)
            };
        }
        private IQueryable<Invoice> ApplyInvoicesFilters(IQueryable<Invoice> query, InvoiceFilterDto filter)
        {
            // Search filter - enhanced to search in multiple fields
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var searchTerm = filter.Search.ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(searchTerm) ||
                    i.Customer.Name.ToLower().Contains(searchTerm) ||
                    (i.Notes != null && i.Notes.ToLower().Contains(searchTerm)) ||
                    (i.Terms != null && i.Terms.ToLower().Contains(searchTerm))
                );
            }

            // Customer filter
            if (filter.CustomerId.HasValue)
                query = query.Where(i => i.CustomerId == filter.CustomerId.Value);

            // Status filter
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(i => i.Status.ToLower() == filter.Status.ToLower());

            // Date range filters
            if (filter.FromDate.HasValue)
                query = query.Where(i => i.IssueDate >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                query = query.Where(i => i.IssueDate <= filter.ToDate.Value);

            // Price range filters
            if (filter.minimumPrice.HasValue)
                query = query.Where(i => i.Total >= filter.minimumPrice.Value);

            if (filter.maximumPrice.HasValue)
                query = query.Where(i => i.Total <= filter.maximumPrice.Value);

            return query;
        }

        #endregion

        #region Invoice Mapping
        private InvoiceDto MapToDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerId = invoice.CustomerId,
                CustomerName = invoice.Customer?.Name,
                CustomerPhoneNumber = invoice.Customer?.Phone,
                IssueDate = invoice.IssueDate,
                DueDate = invoice.DueDate,
                Status = invoice.Status == InvoiceStatus.Pending.ToString() && invoice.DueDate < DateTime.Now
                    ? InvoiceStatus.Overdue.ToString()
                    : invoice.Status,
                Subtotal = invoice.Subtotal,
                VatAmount = invoice.VatAmount,
                TotalDiscount = invoice.TotalDiscount,
                Total = invoice.Total,
                Currency = invoice.Currency,
                InvoiceType = invoice.InvoiceType,
                Notes = invoice.Notes,
                Terms = invoice.Terms,
                AmountPaid = invoice.AmountPaid,
                Benefits = invoice.Benefits,
                DownPayment = invoice.DownPayment,
                hasInstallments = invoice.Installments?.Any() ?? false,
                Items = invoice.InvoiceItems.Select(MapToDto).ToList(),
                Installments = invoice?.Installments?.OrderBy(i => i.DueDate)?.Select(MapToDto).ToList()
            };
        }

        private InvoiceDto MapToDto(Invoice invoice, Company company)
        {
            var invoiceDto = MapToDto(invoice); // Use the existing mapping

            // Add company data to the DTO
            invoiceDto.Company = MapToDto(company);

            return invoiceDto;
        }

        private InvoiceItemDto MapToDto(InvoiceItem item) => new()
        {
            Id = item.Id,
            ItemId = item.ItemId,
            ItemName = item.Item?.Name ?? string.Empty,
            Description = item.Description ?? string.Empty,
            Quantity = item.Quantity ?? 0,
            UnitPrice = item.UnitPrice ?? 0,
            VatRate = item.VatRate ?? 0,
            Discount = item.Discount ?? 0,
            LineTotal = item.LineTotal ?? 0
        };


        private InstallmentDto MapToDto(Installment installment) => new()
        {
            Id = installment.Id,
            InvoiceId = installment.InvoiceId,
            Amount = installment.Amount,
            DueDate = installment.DueDate,
            Status = installment.Status,
            PaidAt = installment.PaidAt
        };
        private CompanyDto MapToDto(Company company) => new()
        {
            Id = company.Id,
            Name = company.Name,
            Address = company.Address,
            Phone = company.Phone,
            Email = company.Email,
            TaxNumber = company.TaxNumber,
            VATNumber = company.VATNumber,
            LogoUrl = GenerateImageWithFolderName(company.LogoUrl),
            Currency = company.Currency,
            DefaultVatRate = company.DefaultVatRate,
            InvoicePrefix = company.InvoicePrefix,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };

        private string GenerateImageWithFolderName(string? imageName)
        {
            var request = _httpContextAccessor?.HttpContext?.Request;
            return request != null && imageName != null
                ? $"{request.Scheme}://{request.Host.Value}{imageName}"
                : string.Empty;
        }

        private Guid GetCurrentTenantId()
        {
            var tenant = _httpContextAccessor.HttpContext?.Items["CurrentTenant"] as Tenant;
            return tenant?.Id ?? Guid.Empty;
        }
        #endregion

        #region Account Statement
        public async Task<ServiceResult<AccountStatementDto>> GetAccountStatementAsync(AccountStatementFilterDto filter)
        {
            try
            {
                // Validate customer exists and is not deleted
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantId
                        && c.Id == filter.CustomerId
                        && !c.IsDeleted);

                if (customer == null)
                {
                    return ServiceResult<AccountStatementDto>.Failure("Customer not found");
                }

                // Get company info for currency
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantId);

                // 1. Calculate Opening Balance (all transactions before start date)
                var openingBalance = await CalculateOpeningBalanceAsync(filter.CustomerId, filter.StartDate, filter.InvoiceType);

                // 2. Get all transactions in the period
                var transactions = new List<AccountStatementTransactionDto>();

                // Add invoice transactions
                var invoiceTransactions = await GetInvoiceTransactionsAsync(filter);
                transactions.AddRange(invoiceTransactions);

                // Add direct payment transactions
                var directPayments = await GetDirectPaymentTransactionsAsync(filter);
                transactions.AddRange(directPayments);

                // Add installment payment transactions
                var installmentPayments = await GetInstallmentPaymentTransactionsAsync(filter);
                transactions.AddRange(installmentPayments);

                // Add payment application records
                var paymentApplications = await GetPaymentApplicationsAsync(filter);
                transactions.AddRange(paymentApplications);

                // 3. Sort transactions and calculate running balance
                var sortedTransactions = transactions
                    .OrderBy(t => t.TransactionDate)
                    .ToList();

                decimal runningBalance = openingBalance;
                foreach (var transaction in sortedTransactions)
                {
                    runningBalance += (transaction.InvoiceAmount ?? 0)
                                    + (transaction.PaymentAmount ?? 0)
                                    + (transaction.CreditAmount ?? 0);
                    transaction.Balance = runningBalance;
                }

                // 4. Add opening balance transaction if there are transactions in the period
                if (sortedTransactions.Any())
                {
                    sortedTransactions.Insert(0, new AccountStatementTransactionDto
                    {
                        TransactionDate = filter.StartDate,
                        Date = filter.StartDate.ToString("dd MMM yyyy"),
                        TransactionType = "Opening Balance",
                        TransactionDetails = "",
                        Balance = openingBalance,
                        OrderPriority = 0
                    });
                }

                var result = new AccountStatementDto
                {
                    CustomerInfo = new CustomerStatementInfoDto
                    {
                        Id = customer.Id,
                        Name = customer.Name,
                        Email = customer.Email,
                        Phone = customer.Phone,
                        Address = customer.Address,
                        AccountType = customer.IsSupplier ? "Supplier" : "Customer",
                        Currency = company?.Currency ?? "USD"
                    },
                    Transactions = sortedTransactions,
                    OpeningBalance = openingBalance,
                    ClosingBalance = runningBalance
                };

                return ServiceResult<AccountStatementDto>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account statement for customer {CustomerId}", filter.CustomerId);
                return ServiceResult<AccountStatementDto>.Failure("Failed to retrieve account statement");
            }
        }

        // Helper method: Calculate opening balance
        private async Task<decimal> CalculateOpeningBalanceAsync(Guid customerId, DateTime startDate, string invoiceType)
        {
            decimal balance = 0;

            // 1️⃣ الفواتير قبل الفترة
            var invoicesBefore = await _context.Invoices
                .Where(i => i.TenantId == _tenantId
                    && i.CustomerId == customerId
                    && i.IssueDate < startDate
                    && i.Status != InvoiceStatus.Cancelled.ToString()
                    && i.InvoiceType == invoiceType)
                .Select(i => invoiceType == InvoiceTypes.Sell.ToString()
                    ? i.Total        // Customer owes us (positive)
                    : -i.Total)      // We owe supplier (negative)
                .SumAsync();

            balance += invoicesBefore;

            // 2️⃣ المدفوعات المباشرة قبل الفترة
            var directPaymentsBefore = await _context.Invoices
                .Where(i => i.TenantId == _tenantId
                    && i.CustomerId == customerId
                    && i.PaidAt.HasValue
                    && i.PaidAt.Value < startDate
                    && i.InvoiceType == invoiceType
                    && i.Status == InvoiceStatus.Paid.ToString())
                .Select(i => invoiceType == InvoiceTypes.Sell.ToString()
                    ? -i.Total      // Payment reduces balance
                    : i.Total)
                .SumAsync();

            balance += directPaymentsBefore;

            // 3️⃣ الأقساط المدفوعة قبل الفترة
            var installmentsBefore = await _context.Installments
                .Where(inst => inst.TenantId == _tenantId
                    && inst.Invoice.CustomerId == customerId
                    && inst.PaidAt.HasValue
                    && inst.PaidAt.Value < startDate
                    && inst.Status == InstallmentStatus.Paid.ToString()
                    && inst.Invoice.InvoiceType == invoiceType)
                .Select(inst => invoiceType == InvoiceTypes.Sell.ToString()
                    ? -inst.Amount
                    : inst.Amount)
                .SumAsync();

            balance += installmentsBefore;

            return balance;
        }

        // Helper method: Get invoice transactions
        private async Task<List<AccountStatementTransactionDto>> GetInvoiceTransactionsAsync(AccountStatementFilterDto filter)
        {
            var invoices = await _context.Invoices
                .Where(i => i.TenantId == _tenantId
                    && i.CustomerId == filter.CustomerId
                    && i.CreatedAt >= filter.StartDate
                    && i.CreatedAt <= filter.EndDate
                    && i.InvoiceType == filter.InvoiceType
                    && i.Status != InvoiceStatus.Cancelled.ToString())
                .Select(i => new AccountStatementTransactionDto
                {
                    TransactionDate = i.CreatedAt,
                    Date = i.CreatedAt.ToString("dd MMM yyyy"),
                    TransactionType = "Invoice",
                    TransactionDetails = i.InvoiceNumber,
                    InvoiceAmount = filter.InvoiceType == InvoiceTypes.Sell.ToString()
                        ? i.Total   // Customer owes us (positive)
                        : -i.Total, // We owe supplier (negative)
                    OrderPriority = 1
                })
                .ToListAsync();

            return invoices;
        }

        // Helper method: Get direct payment transactions
        private async Task<List<AccountStatementTransactionDto>> GetDirectPaymentTransactionsAsync(AccountStatementFilterDto filter)
        {
            var payments = await _context.Invoices
                .Where(i => i.TenantId == _tenantId
                    && i.CustomerId == filter.CustomerId
                    && i.PaidAt.HasValue
                    && i.PaidAt.Value >= filter.StartDate
                    && i.PaidAt.Value <= filter.EndDate
                    && i.InvoiceType == filter.InvoiceType
                    && i.Status == InvoiceStatus.Paid.ToString())
                .Select(i => new AccountStatementTransactionDto
                {
                    TransactionDate = i.PaidAt.Value,
                    Date = i.PaidAt.Value.ToString("dd MMM yyyy"),
                    TransactionType = "Payment Received",
                    TransactionDetails = "PAY-" + i.InvoiceNumber,
                    PaymentAmount = filter.InvoiceType == InvoiceTypes.Sell.ToString()
                                    ? -i.Total
                                    : i.Total, // Payment reduces balance (negative)
                    OrderPriority = 2
                })
                .ToListAsync();

            return payments;
        }

        // Helper method: Get installment payment transactions
        private async Task<List<AccountStatementTransactionDto>> GetInstallmentPaymentTransactionsAsync(AccountStatementFilterDto filter)
        {
            var installmentPayments = await _context.Installments
                .Where(inst => inst.TenantId == _tenantId
                    && inst.Invoice.CustomerId == filter.CustomerId
                    && inst.PaidAt.HasValue
                    && inst.PaidAt.Value >= filter.StartDate
                    && inst.PaidAt.Value <= filter.EndDate
                    && inst.Status == InstallmentStatus.Paid.ToString()
                    && inst.Invoice.InvoiceType == filter.InvoiceType)
                .OrderBy(inst => inst.InvoiceId)
                .ThenBy(inst => inst.DueDate)
                .Select(inst => new
                {
                    inst.PaidAt,
                    inst.Amount,
                    inst.InvoiceId,
                    InvoiceNumber = inst.Invoice.InvoiceNumber,
                    inst.DueDate
                })
                .ToListAsync();

            // Group by invoice to number installments
            var transactions = installmentPayments
                .GroupBy(p => p.InvoiceId)
                .SelectMany(g => g.Select((p, index) => new AccountStatementTransactionDto
                {
                    TransactionDate = p.PaidAt.Value,
                    Date = p.PaidAt.Value.ToString("dd MMM yyyy"),
                    TransactionType = "Payment Received",
                    TransactionDetails = $"INST-{p.InvoiceNumber}-{index + 1}",
                    PaymentAmount = filter.InvoiceType == InvoiceTypes.Sell.ToString()
                        ? -p.Amount   // Customer owes us (positive)
                        : p.Amount, // Payment reduces balance (negative)
                    OrderPriority = 2
                }))
                .ToList();

            return transactions;
        }

        // Helper method: Get payment applications
        private async Task<List<AccountStatementTransactionDto>> GetPaymentApplicationsAsync(AccountStatementFilterDto filter)
        {
            var applications = new List<AccountStatementTransactionDto>();

            // Direct payment applications
            var directApplications = await _context.Invoices
                .Where(i => i.TenantId == _tenantId
                    && i.CustomerId == filter.CustomerId
                    && i.PaidAt.HasValue
                    && i.PaidAt.Value >= filter.StartDate
                    && i.PaidAt.Value <= filter.EndDate
                    && i.InvoiceType == filter.InvoiceType
                    && i.Status == InvoiceStatus.Paid.ToString())
                .Select(i => new AccountStatementTransactionDto
                {
                    TransactionDate = i.PaidAt.Value,
                    Date = i.PaidAt.Value.ToString("dd MMM yyyy"),
                    TransactionType = "Payment Applied",
                    TransactionDetails = $"Applied to invoice {i.InvoiceNumber} for amount {i.Total:N2}",
                    OrderPriority = 3
                })
                .ToListAsync();

            applications.AddRange(directApplications);

            // Installment payment applications
            var installmentApplications = await _context.Installments
                .Where(inst => inst.TenantId == _tenantId
                    && inst.Invoice.CustomerId == filter.CustomerId
                    && inst.PaidAt.HasValue
                    && inst.PaidAt.Value >= filter.StartDate
                    && inst.PaidAt.Value <= filter.EndDate
                    && inst.Status == InstallmentStatus.Paid.ToString()
                    && inst.Invoice.InvoiceType == filter.InvoiceType)
                .Select(inst => new AccountStatementTransactionDto
                {
                    TransactionDate = inst.PaidAt.Value,
                    Date = inst.PaidAt.Value.ToString("dd MMM yyyy"),
                    TransactionType = "Payment Applied",
                    TransactionDetails = $"Applied to invoice {inst.Invoice.InvoiceNumber} for amount {inst.Amount:N2}",
                    PaymentAmount = filter.InvoiceType == InvoiceTypes.Sell.ToString()
                                    ? -inst.Amount
                                    : inst.Amount,
                    OrderPriority = 3
                })
                .ToListAsync();

            applications.AddRange(installmentApplications);

            return applications;
        }
        #endregion

        #region Transactions

        public async Task<ServiceResult<PagedResponseDto<TransactionDto>>> GetRecentTransactionsAsync(
            InvoiceFilterDto filter,
            PaginationDto pagination,
            string? type)
        {
            try
            {
                var query = _context.Transactions
                    .Include(t => t.Project)
                    .Where(t => t.TenantId == _tenantId)
                    .AsQueryable();

                // Apply type filter if provided
                if (!string.IsNullOrEmpty(type))
                {
                    // Map report 'type' to Transaction 'Type' or Direction if needed
                    // For now, assume report 'type' matches Transaction 'Type' or is one of "Sales", "Purchase"
                    if (type == "Sales") query = query.Where(t => t.Type == "PaymentReceived");
                    else if (type == "Purchase") query = query.Where(t => t.Type == "PaymentMade");
                    else query = query.Where(t => t.Type == type);
                }

                // Apply InvoiceFilterDto filters
                if (filter.FromDate.HasValue) query = query.Where(t => t.TransactionDate >= filter.FromDate.Value);
                if (filter.ToDate.HasValue) query = query.Where(t => t.TransactionDate <= filter.ToDate.Value);
                if (filter.BranchId.HasValue) query = query.Where(t => t.BranchId == filter.BranchId.Value);
                if (filter.ProjectId.HasValue) query = query.Where(t => t.ProjectId == filter.ProjectId.Value);
                if (!string.IsNullOrEmpty(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    query = query.Where(t => 
                        (t.Description != null && t.Description.ToLower().Contains(searchTerm)) ||
                        (t.ReferenceId != null && t.ReferenceId.ToLower().Contains(searchTerm))
                    );
                }

                var totalCount = await query.CountAsync();

                // Calculate Statistics
                var totalAmount = await query.SumAsync(t => t.Amount);
                var totalPaid = await query.Where(t => t.Direction == "Credit").SumAsync(t => t.Amount); // Basic logic for paid
                var totalRemaining = 0; // Transactions are usually settlements, so remaining doesn't apply the same as Invoice

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .Select(t => new TransactionDto
                    {
                        TransactionDateTime = t.TransactionDate,
                        Date = t.TransactionDate.ToString("dd MMM yyyy"),
                        Type = t.Type,
                        Reference = t.ReferenceId ?? "TRX",
                        Amount = t.Amount,
                        Paid = t.Amount, // Transactions are movements, so they are fully 'paid' in this context
                        Remaining = 0,
                        Status = "Completed",
                        TargetId = t.ReferenceId,
                        Direction = t.Direction
                    })
                    .ToListAsync();

                var stats = new
                {
                    totalAmount,
                    totalPaid,
                    totalRemaining,
                    totalCount
                };

                return ServiceResult<PagedResponseDto<TransactionDto>>.SuccessResult(
                    new PagedResponseDto<TransactionDto>
                    {
                        Data = transactions,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transactions with filters: {@Filter}", filter);
                return ServiceResult<PagedResponseDto<TransactionDto>>.Failure("Failed to retrieve transactions");
            }
        }

        // Updated filter helper method for invoices
        private IQueryable<Invoice> ApplyInvoicesFilters(IQueryable<Invoice> query, InvoiceFilterDto filter, string? type)
        {
            if (filter.CustomerId.HasValue)
                query = query.Where(i => i.CustomerId == filter.CustomerId);

            // Filter by Sales/Purchase type at database level
            if (!string.IsNullOrWhiteSpace(type))
            {
                if (type == "Sales")
                    query = query.Where(i => i.InvoiceType == InvoiceTypes.Sell.ToString());
                else if (type == "Purchase")
                    query = query.Where(i => i.InvoiceType == InvoiceTypes.Buy.ToString());
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(i =>
                    i.InvoiceNumber.Contains(filter.Search) ||
                    i.Customer.Name.Contains(filter.Search));
            // Status filter
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(i => i.Status.ToLower() == filter.Status.ToLower());


            if (filter.FromDate.HasValue)
                query = query.Where(i => i.CreatedAt >= filter.FromDate);

            if (filter.ToDate.HasValue)
                query = query.Where(i => i.CreatedAt <= filter.ToDate);

            // Price range filters
            if (filter.minimumPrice.HasValue)
                query = query.Where(i => i.Total >= filter.minimumPrice.Value);

            if (filter.maximumPrice.HasValue)
                query = query.Where(i => i.Total <= filter.maximumPrice.Value);

            return query;
        }

        private IQueryable<Installment> ApplyInstallmentFilters(IQueryable<Installment> query, InvoiceFilterDto filter)
        {
            if (filter.CustomerId.HasValue)
                query = query.Where(ins => ins.Invoice.CustomerId == filter.CustomerId);

            if (!string.IsNullOrWhiteSpace(filter.Search))
                query = query.Where(ins =>
                    ins.Invoice.InvoiceNumber.Contains(filter.Search));

            if (filter.FromDate.HasValue)
                query = query.Where(ins => ins.PaidAt >= filter.FromDate);

            if (filter.ToDate.HasValue)
                query = query.Where(ins => ins.PaidAt <= filter.ToDate);


            // Price range filters
            if (filter.minimumPrice.HasValue)
                query = query.Where(i => i.Amount >= filter.minimumPrice.Value);

            if (filter.maximumPrice.HasValue)
                query = query.Where(i => i.Amount <= filter.maximumPrice.Value);

            return query;
        }


        #endregion

        #region Current Stock Report
        public async Task<ServiceResult<PagedResponseDto<CurrentStockReportDto>>> GetCurrentStockReportAsync(
            StockReportFilterDto filter, PaginationDto pagination)
        {
            try
            {
                // Get all items (not deleted)
                var itemsQuery = _context.Items
                    .Where(i => i.TenantId == _tenantId && !i.IsDeleted)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    itemsQuery = itemsQuery.Where(i =>
                        i.Name.ToLower().Contains(searchTerm) ||
                        i.Code.ToLower().Contains(searchTerm));
                }

                if (filter.ItemId.HasValue)
                {
                    itemsQuery = itemsQuery.Where(i => i.Id == filter.ItemId.Value);
                }

                // Get items with their stock calculations
                var stockData = await itemsQuery
                    .Select(item => new
                    {
                        Item = item,
                        CurrentQty = item.Quantity,
                        SoldQty = _context.InvoiceItems
                            .Where(ii => ii.ItemId == item.Id &&
                                        ii.Invoice.TenantId == _tenantId &&
                                        ii.Invoice.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                        ii.Invoice.Status != InvoiceStatus.Cancelled.ToString())
                            .Sum(ii => ii.Quantity ?? 0),

                        AvgPurchasePrice = _context.InvoiceItems
                            .Where(ii => ii.ItemId == item.Id &&
                                        ii.Invoice.TenantId == _tenantId &&
                                        ii.Invoice.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                        ii.Invoice.Status != InvoiceStatus.Cancelled.ToString())
                            .Average(ii => ii.UnitPrice) ?? item.PurchaseUnitPrice,

                        AvgSellPrice = _context.InvoiceItems
                            .Where(ii => ii.ItemId == item.Id &&
                                        ii.Invoice.TenantId == _tenantId &&
                                        ii.Invoice.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                        ii.Invoice.Status != InvoiceStatus.Cancelled.ToString())
                            .Average(ii => ii.UnitPrice) ?? item.UnitPrice
                    })
                    .ToListAsync();

                // Map to DTO and calculate in stock
                var reportData = stockData
                    .Select(s => new CurrentStockReportDto
                    {
                        ItemId = s.Item.Id,
                        ItemCode = s.Item.Code,
                        ItemName = s.Item.Name,
                        SoldQty = s.SoldQty,
                        InStock = s.CurrentQty ?? 0,
                        PurchasePrice = s.AvgPurchasePrice,
                        SellPrice = s.AvgSellPrice
                    })
                    .ToList();

                // Apply low stock filter if needed
                if (filter.LowStock == true)
                {
                    reportData = reportData
                        .Where(r => r.InStock <= 10) // You can make this configurable
                        .ToList();
                }

                // Get total count
                var totalCount = reportData.Count;

                // Apply pagination
                var pagedData = reportData
                    .OrderBy(r => r.ItemName)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToList();

                // Calculate summary statistics
                var stats = new
                {
                    totalItems = totalCount,
                    totalInStock = reportData.Sum(r => r.InStock),
                    totalValue = itemsQuery.Where(i => i.TenantId == _tenantId && i.IsActive && !i.IsDeleted && i.Quantity.HasValue && i.PurchaseUnitPrice.HasValue).Sum(i => i.Quantity.Value * i.PurchaseUnitPrice.Value),
                    lowStockItems = reportData.Count(r => r.InStock <= 10)
                };

                return ServiceResult<PagedResponseDto<CurrentStockReportDto>>.SuccessResult(
                    new PagedResponseDto<CurrentStockReportDto>
                    {
                        Data = pagedData,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current stock report");
                return ServiceResult<PagedResponseDto<CurrentStockReportDto>>.Failure("Failed to retrieve stock report");
            }
        }
        #endregion

        #region Item Movement Report
        public async Task<ServiceResult<List<ItemMovementReportDto>>> GetItemMovementReportAsync(
    ItemMovementFilterDto filter)
        {
            try
            {
                // Load item
                var item = await _context.Items
                    .FirstOrDefaultAsync(i =>
                        i.Id == filter.ItemId &&
                        i.TenantId == _tenantId &&
                        !i.IsDeleted);

                if (item == null)
                {
                    return ServiceResult<List<ItemMovementReportDto>>.Failure("Item not found");
                }

                // Opening quantity (initial stock). Change 'InitialQuantity' to your field.
                decimal openingQty = item.InitialQuantity ?? 0;

                // Prepare movement list
                var report = new List<ItemMovementReportDto>();

                // Add opening balance only if > 0
                if (openingQty > 0)
                {
                    report.Add(new ItemMovementReportDto
                    {
                        Date = item.CreatedAt,      // Use CreatedAt or Min(filter.FromDate)
                        InvoiceNumber = "—",
                        Type = "Opening",
                        QtyIn = openingQty,
                        QtyOut = 0,
                        Balance = openingQty,
                        UnitPrice = 0,
                        CurrentBalance = item.Quantity
                    });
                }

                // Running balance starts from opening stock
                decimal runningBalance = openingQty;

                // Get invoice item movements
                var movementsQuery = _context.InvoiceItems
                    .Include(ii => ii.Invoice)
                    .Where(ii =>
                        ii.ItemId == filter.ItemId &&
                        ii.Invoice.TenantId == _tenantId &&
                       (ii.Invoice.Status.ToLower() == InvoiceStatus.Paid.ToString().ToLower() || ii.Invoice.Status.ToLower() == InvoiceStatus.PartialPaid.ToString().ToLower() || ii.Invoice.Status.ToLower() == InvoiceStatus.Pending.ToString().ToLower() || ii.Invoice.Status.ToLower() == InvoiceStatus.Overdue.ToString().ToLower()))
                    .AsQueryable();

                // Apply filters
                if (filter.FromDate.HasValue)
                {
                    movementsQuery = movementsQuery
                        .Where(ii => ii.Invoice.IssueDate >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    movementsQuery = movementsQuery
                        .Where(ii => ii.Invoice.IssueDate <= filter.ToDate.Value);
                }

                // Fetch sorted movements
                var movements = await movementsQuery
                    .OrderBy(ii => ii.Invoice.IssueDate)
                    .ThenBy(ii => ii.Invoice.InvoiceNumber)
                    .Select(ii => new
                    {
                        Date = ii.Invoice.IssueDate,
                        InvoiceNumber = ii.Invoice.InvoiceNumber,
                        Type = ii.Invoice.InvoiceType,
                        Quantity = ii.Quantity ?? 0,
                        UnitPrice = ii.UnitPrice ?? 0,
                        CurrentBalance = item.Quantity
                    })
                    .ToListAsync();

                // Calculate movement flow
                foreach (var movement in movements)
                {
                    bool isBuy = movement.Type == InvoiceTypes.Buy.ToString();
                    decimal qtyIn = isBuy ? movement.Quantity : 0;
                    decimal qtyOut = isBuy ? 0 : movement.Quantity;

                    runningBalance += qtyIn - qtyOut;

                    report.Add(new ItemMovementReportDto
                    {
                        Date = movement.Date,
                        InvoiceNumber = movement.InvoiceNumber,
                        Type = movement.Type,
                        QtyIn = qtyIn,
                        QtyOut = qtyOut,
                        Balance = runningBalance,
                        UnitPrice = movement.UnitPrice,
                        CurrentBalance = item.Quantity
                    });
                }

                return ServiceResult<List<ItemMovementReportDto>>.SuccessResult(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item movement report for item {ItemId}", filter.ItemId);
                return ServiceResult<List<ItemMovementReportDto>>.Failure("Failed to retrieve item movement report");
            }
        }

        #endregion

        #region Item Profitability Report
        public async Task<ServiceResult<PagedResponseDto<ItemProfitabilityReportDto>>> GetItemProfitabilityReportAsync(
                ItemProfitabilityFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var itemsQuery = _context.Items
                    .Where(i => i.TenantId == _tenantId && !i.IsDeleted)
                    .AsQueryable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    itemsQuery = itemsQuery.Where(i =>
                        i.Name.ToLower().Contains(searchTerm) ||
                        i.Code.ToLower().Contains(searchTerm));
                }

                // Get profitability data
                var profitabilityData = await itemsQuery
                    .Select(item => new
                    {
                        Item = item,
                        // Total sales (only paid/partially paid invoices)
                        TotalSales = _context.InvoiceItems
                            .Where(ii => ii.ItemId == item.Id &&
                                        ii.Invoice.TenantId == _tenantId &&
                                        ii.Invoice.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                        (ii.Invoice.Status == InvoiceStatus.Paid.ToString() ||
                                         ii.Invoice.Status == InvoiceStatus.PartialPaid.ToString() ||
                                         ii.Invoice.Status == InvoiceStatus.Pending.ToString() ||
                                         ii.Invoice.Status == InvoiceStatus.Overdue.ToString()) &&
                                        (!filter.FromDate.HasValue || ii.Invoice.IssueDate >= filter.FromDate.Value) &&
                                        (!filter.ToDate.HasValue || ii.Invoice.IssueDate <= filter.ToDate.Value))
                            .Sum(ii => ii.LineTotal ?? 0),

                        // Total cost (purchases)
                        TotalCost = _context.InvoiceItems
                            .Where(ii => ii.ItemId == item.Id &&
                                        ii.Invoice.TenantId == _tenantId &&
                                        ii.Invoice.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                        (ii.Invoice.Status == InvoiceStatus.Paid.ToString() ||
                                         ii.Invoice.Status == InvoiceStatus.PartialPaid.ToString() ||
                                         ii.Invoice.Status == InvoiceStatus.Pending.ToString() ||
                                         ii.Invoice.Status == InvoiceStatus.Overdue.ToString()) &&
                                        (!filter.FromDate.HasValue || ii.Invoice.IssueDate >= filter.FromDate.Value) &&
                                        (!filter.ToDate.HasValue || ii.Invoice.IssueDate <= filter.ToDate.Value))
                            .Sum(ii => ii.LineTotal ?? 0)
                    })
                    .ToListAsync();

                // Calculate profit and map to DTO
                var reportData = profitabilityData
                    .Where(p => p.TotalSales > 0 || p.TotalCost > 0) // Only items with transactions
                    .Select(p =>
                    {
                        var profit = p.TotalSales - p.TotalCost;
                        var profitPercentage = p.TotalSales > 0 ? (profit / p.TotalSales) * 100 : 0;

                        return new ItemProfitabilityReportDto
                        {
                            ItemId = p.Item.Id,
                            ItemCode = p.Item.Code,
                            ItemName = p.Item.Name,
                            TotalSales = p.TotalSales,
                            TotalCost = p.TotalCost,
                            Profit = profit,
                            ProfitPercentage = profitPercentage
                        };
                    })
                    .OrderByDescending(r => r.Profit)
                    .ToList();

                // Apply top count filter if specified
                if (filter.TopCount.HasValue && filter.TopCount.Value > 0)
                {
                    reportData = reportData.Take(filter.TopCount.Value).ToList();
                }

                // Get total count
                var totalCount = reportData.Count;

                // Apply pagination
                var pagedData = reportData
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToList();

                // Calculate summary statistics
                var stats = new
                {
                    totalRevenue = reportData.Sum(r => r.TotalSales),
                    totalCost = reportData.Sum(r => r.TotalCost),
                    totalProfit = reportData.Sum(r => r.Profit),
                    avgProfitMargin = reportData.Any() ? reportData.Average(r => r.ProfitPercentage) : 0
                };

                return ServiceResult<PagedResponseDto<ItemProfitabilityReportDto>>.SuccessResult(
                    new PagedResponseDto<ItemProfitabilityReportDto>
                    {
                        Data = pagedData,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item profitability report");
                return ServiceResult<PagedResponseDto<ItemProfitabilityReportDto>>.Failure("Failed to retrieve profitability report");
            }
        }
        #endregion
        #region Project Sheet
        public async Task<ServiceResult<ProjectSheetDto>> GetProjectSheetAsync(Guid projectId, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                if (project == null) return ServiceResult<ProjectSheetDto>.Failure("Project not found");

                var query = _context.Transactions
                    .Where(t => t.TenantId == _tenantId && t.ProjectId == projectId)
                    .AsQueryable();

                if (fromDate.HasValue) query = query.Where(t => t.TransactionDate >= fromDate.Value);
                if (toDate.HasValue) query = query.Where(t => t.TransactionDate <= toDate.Value);

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => new fatortak.Dtos.Transaction.TransactionDto
                    {
                        Id = t.Id,
                        Date = t.TransactionDate.ToString("dd MMM yyyy"),
                        TransactionDate = t.TransactionDate,
                        Type = t.Type,
                        Amount = t.Amount,
                        Direction = t.Direction,
                        Description = t.Description,
                        Reference = t.ReferenceId,
                        ReferenceId = t.ReferenceId,
                        Category = t.Category,
                        AttachmentUrl = t.AttachmentUrl
                    })
                    .ToListAsync();

                var totalIncome = transactions.Where(t => t.Direction == "Credit").Sum(t => t.Amount);
                var totalExpenses = transactions.Where(t => t.Direction == "Debit").Sum(t => t.Amount);

                // Calculate Receivables (Unpaid Invoices linked to Project)
                var receivablesQuery = _context.Invoices
                    .Where(i => i.TenantId == _tenantId && i.ProjectId == projectId && i.InvoiceType == InvoiceTypes.Sell.ToString() && i.Status != InvoiceStatus.Paid.ToString() && i.Status != InvoiceStatus.Cancelled.ToString());
                
                if (fromDate.HasValue) receivablesQuery = receivablesQuery.Where(i => i.IssueDate >= fromDate.Value);
                if (toDate.HasValue) receivablesQuery = receivablesQuery.Where(i => i.IssueDate <= toDate.Value);

                var totalReceivables = await receivablesQuery
                    .SumAsync(i => i.Total - (i.AmountPaid ?? 0));

                return ServiceResult<ProjectSheetDto>.SuccessResult(new ProjectSheetDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    TotalIncome = totalIncome,
                    TotalReceivables = totalReceivables,
                    TotalExpenses = totalExpenses,
                    NetProfit = totalIncome - totalExpenses,
                    Transactions = transactions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project sheet");
                return ServiceResult<ProjectSheetDto>.Failure("Failed to retrieve project sheet");
            }
        }
        #endregion

        #region Treasury Report
        public async Task<ServiceResult<TreasuryReportDto>> GetTreasuryReportAsync(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                // 1. Get Accounts Balances (This report is now defunct or needs redesign)
                var accounts = new List<AccountBalanceDto>();

                // 2. Get Transactions
                var transactionsQuery = _context.Transactions
                    .Where(t => t.TenantId == _tenantId)
                    .AsQueryable();

                var transactions = await transactionsQuery
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => new fatortak.Dtos.Transaction.TransactionDto
                    {
                        Id = t.Id,
                        Date = t.TransactionDate.ToString("dd MMM yyyy"),
                        TransactionDate = t.TransactionDate,
                        Type = t.Type,
                        Amount = t.Amount,
                        Direction = t.Direction,
                        Description = t.Description,
                        Reference = t.ReferenceId,
                        ReferenceId = t.ReferenceId,
                        Category = t.Category
                    })
                    .ToListAsync();

                return ServiceResult<TreasuryReportDto>.SuccessResult(new TreasuryReportDto
                {
                    TotalBalance = accounts.Sum(a => a.Balance),
                    Accounts = accounts,
                    Transactions = transactions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting treasury report");
                return ServiceResult<TreasuryReportDto>.Failure("Failed to retrieve treasury report");
            }
        }
        #endregion

        #region Supplier Ledger
        public async Task<ServiceResult<AccountStatementDto>> GetSupplierLedgerAsync(Guid supplierId, DateTime? fromDate, DateTime? toDate)
        {
            // Reusing Account Statement Logic with InvoiceType = Buy
            var filter = new AccountStatementFilterDto
            {
                CustomerId = supplierId,
                StartDate = fromDate ?? DateTime.MinValue, // Default to all time if null? Or sensible default
                EndDate = toDate ?? DateTime.UtcNow,
                InvoiceType = InvoiceTypes.Buy.ToString()
            };

            // If dates are null, AccountStatement might default. Let's ensure sensible defaults if needed or let logic handle it.
            // StartDate in AccountStatement used for "Opening Balance". If MinValue, Opening is 0.
            
            return await GetAccountStatementAsync(filter);
        }
        #endregion
    }
}
