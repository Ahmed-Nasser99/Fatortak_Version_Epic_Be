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
        public async Task<ServiceResult<ReportStatsDto>> GetReportStatsAsync(string period = "month", Guid? projectId = null)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // Financial stats based on account balances
                var linesQuery = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= startDate &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId));

                // ✅ Revenue (AccountType.Revenue)
                var revenue = await linesQuery
                    .Where(jel => jel.Account.AccountType == AccountType.Revenue)
                    .SumAsync(jel => jel.Credit - jel.Debit);

                // ✅ Expenses (AccountType.Expense)
                var totalExpenses = await linesQuery
                    .Where(jel => jel.Account.AccountType == AccountType.Expense)
                    .SumAsync(jel => jel.Debit - jel.Credit);

                // ✅ AR (Accounts Receivable - Code 1200)
                var receivables = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountCode.StartsWith("1200"))
                    .SumAsync(jel => jel.Debit - jel.Credit);

                var invoices = await _context.Invoices
                    .CountAsync(i => i.TenantId == _tenantId &&
                                     i.IssueDate >= startDate &&
                                     i.IssueDate <= endDate &&
                                     (!projectId.HasValue || i.ProjectId == projectId));

                var customers = await _context.Customers
                    .CountAsync(c => c.TenantId == _tenantId && !c.IsSupplier && c.IsActive && !c.IsDeleted);

                var suppliers = await _context.Customers
                    .CountAsync(c => c.TenantId == _tenantId && c.IsSupplier && c.IsActive && !c.IsDeleted);

                return ServiceResult<ReportStatsDto>.SuccessResult(new ReportStatsDto
                {
                    TotalRevenue = revenue,
                    TotalExpenses = totalExpenses,
                    NetIncome = revenue - totalExpenses,
                    TotalInvoices = invoices,
                    ActiveCustomers = customers,
                    ActiveSuppliers = suppliers
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
        public async Task<ServiceResult<List<RevenueDataPointDto>>> GetRevenueDataAsync(string period = "month", Guid? projectId = null)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                var query = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= startDate &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountType == AccountType.Revenue);

                List<RevenueDataPointDto> data;

                if (period == "month")
                {
                    data = await query
                        .GroupBy(jel => jel.JournalEntry.Date)
                        .Select(g => new RevenueDataPointDto
                        {
                            Period = g.Key.ToString("dd MMM"),
                            Revenue = g.Sum(jel => jel.Credit - jel.Debit),
                            Orders = g.Select(jel => jel.JournalEntryId).Distinct().Count()
                        })
                        .ToListAsync();
                }
                else
                {
                    data = await query
                        .GroupBy(jel => new { jel.JournalEntry.Date.Year, jel.JournalEntry.Date.Month })
                        .Select(g => new RevenueDataPointDto
                        {
                            Period = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month),
                            Revenue = g.Sum(jel => jel.Credit - jel.Debit),
                            Orders = g.Select(jel => jel.JournalEntryId).Distinct().Count()
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
        public async Task<ServiceResult<List<TopCustomerDto>>> GetTopCustomersAsync(string period = "month", int topCount = 5, Guid? projectId = null)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // ✅ Ranking by Revenue generated (Accrual basis)
                // Join Revenue Journal Entries with Invoices to get Customer info
                var result = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= startDate &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountType == AccountType.Revenue &&
                                  jel.JournalEntry.ReferenceType == JournalEntryReferenceType.Invoice)
                    .Join(_context.Invoices.Include(i => i.Customer),
                          jel => jel.JournalEntry.ReferenceId,
                          i => i.Id,
                          (jel, i) => new { jel, i })
                    .Where(x => x.i.InvoiceType == InvoiceTypes.Sell.ToString())
                    .GroupBy(x => new { x.i.CustomerId, x.i.Customer.Name })
                    .Select(g => new TopCustomerDto
                    {
                        Id = g.Key.CustomerId,
                        Name = g.Key.Name,
                        Orders = g.Select(x => x.i.Id).Distinct().Count(),
                        TotalSpent = g.Sum(x => x.jel.Credit - x.jel.Debit),
                        LastOrderDate = g.Max(x => x.i.IssueDate),
                        Status = g.Sum(x => x.jel.Credit - x.jel.Debit) > 10000 ? "VIP" : "Regular"
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
        public async Task<ServiceResult<List<TopSupplierDto>>> GetTopSuppliersAsync(string period = "month", int topCount = 5, Guid? projectId = null)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // ✅ Ranking by Cost incurred (Accrual basis)
                // Includes Buy Invoices and Expenses
                var result = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= startDate &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountType == AccountType.Expense)
                    .Select(jel => new
                    {
                        jel.Debit,
                        jel.Credit,
                        jel.JournalEntry.ReferenceType,
                        jel.JournalEntry.ReferenceId,
                        jel.JournalEntry.Description
                    })
                    .ToListAsync(); // Pull to memory to handle complex joins/regex if needed, or use subqueries

                // Since TopSupplier needs to group by SupplierId which is in Invoices or Expenses, 
                // and ReferenceId might be Guid? or int (for Expense), we need careful mapping.
                
                // For now, let's keep it simpler but accounting-based by focusing on Invoice-based suppliers first
                // and then adding direct expenses if they have a linked supplier (not implemented yet in schema).

                var suppliersInvoiced = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= startDate &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountCode.StartsWith("5000") && // Expense accounts
                                  jel.JournalEntry.ReferenceType == JournalEntryReferenceType.Invoice)
                    .Join(_context.Invoices.Include(i => i.Customer),
                          jel => jel.JournalEntry.ReferenceId,
                          i => i.Id,
                          (jel, i) => new { jel, i })
                    .Where(x => x.i.InvoiceType == InvoiceTypes.Buy.ToString())
                    .GroupBy(x => new { x.i.CustomerId, x.i.Customer.Name })
                    .Select(g => new TopSupplierDto
                    {
                        Id = g.Key.CustomerId,
                        Name = g.Key.Name,
                        Orders = g.Select(x => x.i.Id).Distinct().Count(),
                        TotalAmount = g.Sum(x => x.jel.Debit - x.jel.Credit),
                        LastOrderDate = g.Max(x => x.i.IssueDate)
                    })
                    .OrderByDescending(s => s.TotalAmount)
                    .Take(topCount)
                    .ToListAsync();

                return ServiceResult<List<TopSupplierDto>>.SuccessResult(suppliersInvoiced);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top suppliers");
                return ServiceResult<List<TopSupplierDto>>.Failure("Failed to retrieve top suppliers");
            }
        }
        #endregion

        #region Cash Flow
        public async Task<ServiceResult<CashFlowDto>> GetCashFlowAsync(string period = "month", Guid? projectId = null)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // Financial lines for the period
                var linesQuery = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= startDate &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId));

                // ✅ Cash In = Collections (Credits to AR) + Direct Cash Sales (Debits to Cash from Revenue)
                var cashIn = await linesQuery
                    .Where(jel => jel.Account.AccountCode.StartsWith("1200"))
                    .SumAsync(jel => jel.Credit);

                // ✅ Cash Out = Payments (Debits to AP) + Direct Cash Expenses (Credits to Cash from Expense)
                var cashOut = await linesQuery
                    .Where(jel => jel.Account.AccountCode.StartsWith("2100"))
                    .SumAsync(jel => jel.Debit);

                // ✅ Outstanding Receivables (AR - AccountCode 1200, cumulative)
                var outstanding = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date <= endDate && 
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountCode.StartsWith("1200"))
                    .SumAsync(jel => jel.Debit - jel.Credit);

                return ServiceResult<CashFlowDto>.SuccessResult(new CashFlowDto
                {
                    CashIn = cashIn,
                    CashOut = cashOut,
                    NetCashFlow = cashIn - cashOut,
                    TotalExpenses = await linesQuery
                        .Where(jel => jel.Account.AccountType == AccountType.Expense)
                        .SumAsync(jel => jel.Debit - jel.Credit),
                    TotalPurchaseInvoices = 0,
                    TotalSalaries = 0,
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
        public async Task<ServiceResult<ProfitAnalysisDto>> GetProfitAnalysisAsync(string period = "month", Guid? projectId = null)
        {
            try
            {
                var (startDate, endDate) = GetDateRange(period);

                // Financial lines for the period
                var linesQuery = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= startDate &&
                                  jel.JournalEntry.Date <= endDate &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId));

                // ✅ Revenue (AccountType.Revenue: Credit - Debit)
                var revenue = await linesQuery
                    .Where(jel => jel.Account.AccountType == AccountType.Revenue)
                    .SumAsync(jel => jel.Credit - jel.Debit);

                // ✅ Expenses (AccountType.Expense: Debit - Credit)
                var totalExpenses = await linesQuery
                    .Where(jel => jel.Account.AccountType == AccountType.Expense)
                    .SumAsync(jel => jel.Debit - jel.Credit);

                var netProfit = revenue - totalExpenses;
                var grossMargin = revenue > 0 ? (netProfit / revenue) * 100 : 0;

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
        public async Task<ServiceResult<PagedResponseDto<TransactionDto>>> GetSalesReport(
        InvoiceFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var arAccountCode = "1200"; // Accounts Receivable

                var baseQuery = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .ThenInclude(je => je.Project)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.Account.AccountCode.StartsWith(arAccountCode))
                    .AsQueryable();

                // Join with Invoices to apply invoice-specific filters (like CustomerId)
                // Note: We use a left join/Any check for payments that might not be directly linked to a specific invoice ID but linked to a project
                var filteredQuery = baseQuery.Where(jel => 
                    (jel.JournalEntry.ReferenceId != null && _context.Invoices.Any(i => i.Id == jel.JournalEntry.ReferenceId && i.InvoiceType == InvoiceTypes.Sell.ToString())) ||
                    (jel.JournalEntry.ProjectId != null && _context.Projects.Any(p => p.Id == jel.JournalEntry.ProjectId))
                );

                if (filter.CustomerId.HasValue)
                {
                    filteredQuery = filteredQuery.Where(jel => 
                        (jel.JournalEntry.ReferenceId != null && _context.Invoices.Any(i => i.Id == jel.JournalEntry.ReferenceId && i.CustomerId == filter.CustomerId)) ||
                        (jel.JournalEntry.ProjectId != null && _context.Projects.Any(p => p.Id == jel.JournalEntry.ProjectId && p.CustomerId == filter.CustomerId))
                    );
                }

                if (filter.FromDate.HasValue)
                {
                    filteredQuery = filteredQuery.Where(jel => jel.JournalEntry.Date >= filter.FromDate.Value);
                }
                if (filter.ToDate.HasValue)
                {
                    filteredQuery = filteredQuery.Where(jel => jel.JournalEntry.Date <= filter.ToDate.Value);
                }
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    filteredQuery = filteredQuery.Where(jel => 
                        (jel.JournalEntry.Description != null && jel.JournalEntry.Description.ToLower().Contains(searchTerm)) ||
                        (jel.JournalEntry.EntryNumber.ToLower().Contains(searchTerm))
                    );
                }

                // Get total count for pagination
                var totalCount = await filteredQuery.CountAsync();

                // Calculate statistics
                // In a Sales Report context:
                // Debits to AR are "Sales" (Invoices)
                // Credits to AR are "Payments"
                var stats = new
                {
                    totalSales = await filteredQuery.SumAsync(jel => jel.Debit),
                    totalPaid = await filteredQuery.SumAsync(jel => jel.Credit),
                    totalReceivables = await filteredQuery.SumAsync(jel => jel.Debit - jel.Credit),
                    totalCount = totalCount
                };

                var lines = await filteredQuery
                    .OrderByDescending(jel => jel.JournalEntry.Date)
                    .ThenByDescending(jel => jel.JournalEntry.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var transactionDtos = lines.Select(l => new TransactionDto
                {
                    Id = l.JournalEntryId,
                    TransactionDate = l.JournalEntry.Date,
                    Date = l.JournalEntry.Date.ToString("dd MMM yyyy"),
                    Type = l.JournalEntry.ReferenceType.ToString(),
                    Amount = l.Debit > 0 ? l.Debit : l.Credit,
                    Direction = l.Debit > 0 ? "Debit" : "Credit",
                    Description = l.JournalEntry.Description,
                    ReferenceId = l.JournalEntry.ReferenceId?.ToString(),
                    ReferenceType = l.JournalEntry.ReferenceType.ToString(),
                    ProjectName = l.JournalEntry.Project?.Name,
                    CreatedAt = l.JournalEntry.CreatedAt
                }).ToList();

                return ServiceResult<PagedResponseDto<TransactionDto>>.SuccessResult(
                    new PagedResponseDto<TransactionDto>
                    {
                        Data = transactionDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount,
                        MetaData = stats
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sales report with filters: {@Filter}", filter);
                return ServiceResult<PagedResponseDto<TransactionDto>>.Failure("Failed to retrieve sales report");
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

            // Project filter
            if (filter.ProjectId.HasValue)
                query = query.Where(e => e.ProjectId == filter.ProjectId.Value);

            // Branch filter
            if (filter.BranchId.HasValue)
                query = query.Where(e => e.BranchId == filter.BranchId.Value);

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

            // Project filter
            if (filter.ProjectId.HasValue)
                query = query.Where(i => i.ProjectId == filter.ProjectId.Value);

            // Branch filter
            if (filter.BranchId.HasValue)
                query = query.Where(i => i.BranchId == filter.BranchId.Value);

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
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantId
                        && c.Id == filter.CustomerId
                        && !c.IsDeleted);

                if (customer == null) return ServiceResult<AccountStatementDto>.Failure("Customer not found");

                var company = await _context.Companies.FirstOrDefaultAsync(c => c.TenantId == _tenantId);
                var arAccountCode = filter.InvoiceType == InvoiceTypes.Sell.ToString() ? "1200" : "2100";

                // 1. Calculate Opening Balance from Journal Entries
                var openingBalance = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date < filter.StartDate &&
                                  jel.Account.AccountCode.StartsWith(arAccountCode))
                    .Where(jel => 
                        (jel.JournalEntry.ReferenceId != null && _context.Invoices.Any(i => i.Id == jel.JournalEntry.ReferenceId && i.CustomerId == filter.CustomerId && (i.InvoiceType == filter.InvoiceType || (filter.InvoiceType == "Sell" && (i.InvoiceType == "Sales" || i.InvoiceType == "Sale"))))) ||
                        (jel.JournalEntry.ProjectId != null && _context.Projects.Any(p => p.Id == jel.JournalEntry.ProjectId && p.CustomerId == filter.CustomerId))
                    )
                    .SumAsync(jel => jel.Debit - jel.Credit);

                // For suppliers, balance is usually Credit - Debit, but we want a signed representation
                if (filter.InvoiceType == InvoiceTypes.Buy.ToString())
                {
                    openingBalance = -openingBalance; // We use negative for what we owe suppliers in this report's logic
                }

                // 2. Get all transactions in the period
                // We join JournalEntryLines with Invoices to filter by customer
                var query = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .ThenInclude(je => je.Project)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= filter.StartDate &&
                                  jel.JournalEntry.Date <= filter.EndDate &&
                                  jel.Account.AccountCode.StartsWith(arAccountCode))
                    .Where(jel => 
                        (jel.JournalEntry.ReferenceId != null && _context.Invoices.Any(i => i.Id == jel.JournalEntry.ReferenceId && i.CustomerId == filter.CustomerId && (i.InvoiceType == filter.InvoiceType || (filter.InvoiceType == "Sell" && (i.InvoiceType == "Sales" || i.InvoiceType == "Sale"))))) ||
                        (jel.JournalEntry.ProjectId != null && _context.Projects.Any(p => p.Id == jel.JournalEntry.ProjectId && p.CustomerId == filter.CustomerId))
                    )
                    .OrderBy(jel => jel.JournalEntry.Date)
                    .Select(jel => new AccountStatementTransactionDto
                    {
                        TransactionDate = jel.JournalEntry.Date,
                        Date = jel.JournalEntry.Date.ToString("dd MMM yyyy"),
                        TransactionType = jel.JournalEntry.ReferenceType.ToString(),
                        TransactionDetails = jel.JournalEntry.Description ?? "",
                        ProjectId = jel.JournalEntry.ProjectId,
                        ProjectName = jel.JournalEntry.Project != null ? jel.JournalEntry.Project.Name : null,
                        // If it's an Invoice JE, it adds to AR. If it's a Payment JE, it reduces AR.
                        InvoiceAmount = jel.JournalEntry.ReferenceType == JournalEntryReferenceType.Invoice ? ((filter.InvoiceType == InvoiceTypes.Sell.ToString() || filter.InvoiceType == "Sales" || filter.InvoiceType == "Sale") ? jel.Debit : -jel.Credit) : 0,
                        PaymentAmount = jel.JournalEntry.ReferenceType == JournalEntryReferenceType.Payment ? ((filter.InvoiceType == InvoiceTypes.Sell.ToString() || filter.InvoiceType == "Sales" || filter.InvoiceType == "Sale") ? -jel.Credit : jel.Debit) : 0,
                        OrderPriority = jel.JournalEntry.ReferenceType == JournalEntryReferenceType.Invoice ? 1 : 2
                    });

                var transactions = await query.ToListAsync();

                // 3. Sort and calculate running balance
                var sortedTransactions = transactions
                    .OrderBy(t => t.TransactionDate)
                    .ThenBy(t => t.OrderPriority)
                    .ToList();

                decimal runningBalance = openingBalance;
                foreach (var transaction in sortedTransactions)
                {
                    runningBalance += (transaction.InvoiceAmount ?? 0) + (transaction.PaymentAmount ?? 0);
                    transaction.Balance = runningBalance;
                }

                // 4. Add opening balance row
                sortedTransactions.Insert(0, new AccountStatementTransactionDto
                {
                    TransactionDate = filter.StartDate,
                    Date = filter.StartDate.ToString("dd MMM yyyy"),
                    TransactionType = "Opening Balance",
                    TransactionDetails = "",
                    Balance = openingBalance,
                    OrderPriority = 0
                });

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

        #endregion

        #region Transactions

        public async Task<ServiceResult<PagedResponseDto<TransactionDto>>> GetRecentTransactionsAsync(
            InvoiceFilterDto filter,
            PaginationDto pagination,
            string? type)
        {
            try
            {
                var query = _context.JournalEntries
                    .Include(je => je.Lines)
                    .ThenInclude(l => l.Account)
                    .Include(je => je.Project)
                    .Where(je => je.TenantId == _tenantId && je.IsPosted)
                    .AsQueryable();

                // Apply type filter based on ReferenceType
                if (!string.IsNullOrEmpty(type))
                {
                    if (type == "Sales") query = query.Where(je => je.ReferenceType == JournalEntryReferenceType.Invoice);
                    else if (type == "Payment") query = query.Where(je => je.ReferenceType == JournalEntryReferenceType.Payment);
                    else if (type == "Expense") query = query.Where(je => je.ReferenceType == JournalEntryReferenceType.Expense);
                }

                if (filter.FromDate.HasValue) query = query.Where(je => je.Date >= filter.FromDate.Value);
                if (filter.ToDate.HasValue) query = query.Where(je => je.Date <= filter.ToDate.Value);
                if (filter.ProjectId.HasValue) query = query.Where(je => je.ProjectId == filter.ProjectId.Value);

                if (!string.IsNullOrEmpty(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    query = query.Where(je => 
                        (je.Description != null && je.Description.ToLower().Contains(searchTerm)) ||
                        (je.EntryNumber != null && je.EntryNumber.ToLower().Contains(searchTerm)));
                }

                var totalCount = await query.CountAsync();
                var journalEntries = await query
                    .OrderByDescending(je => je.Date)
                    .ThenByDescending(je => je.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var transactions = journalEntries.Select(je =>
                {
                    var totalDebit = je.Lines.Sum(l => l.Debit);
                    
                    string transactionType = je.ReferenceType.ToString();
                    decimal amount = totalDebit;
                    decimal paid = (je.ReferenceType == JournalEntryReferenceType.Payment || je.ReferenceType == JournalEntryReferenceType.Expense) ? totalDebit : 0;
                    
                    return new TransactionDto
                    {
                        Id = je.Id,
                        TransactionDate = je.Date,
                        Date = je.Date.ToString("dd MMM yyyy"),
                        Type = transactionType,
                        Reference = je.Description ?? je.EntryNumber,
                        Amount = amount,
                        // Using TransactionDto fields correctly
                        ReferenceId = je.ReferenceId.ToString(),
                        ReferenceType = je.ReferenceType.ToString(),
                        Description = je.Description,
                        ProjectName = je.Project?.Name,
                        CreatedAt = je.CreatedAt
                    };
                }).ToList();

                return ServiceResult<PagedResponseDto<TransactionDto>>.SuccessResult(new PagedResponseDto<TransactionDto>
                {
                    Data = transactions,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent transactions");
                return ServiceResult<PagedResponseDto<TransactionDto>>.Failure("Failed to retrieve recent transactions");
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

                var journalEntries = await _context.JournalEntries
                    .Include(je => je.Lines)
                    .ThenInclude(l => l.Account)
                    .Where(je => je.TenantId == _tenantId && je.ProjectId == projectId && je.IsPosted)
                    .AsQueryable()
                    .OrderByDescending(je => je.Date)
                    .ToListAsync();

                if (fromDate.HasValue) journalEntries = journalEntries.Where(je => je.Date >= fromDate.Value).ToList();
                if (toDate.HasValue) journalEntries = journalEntries.Where(je => je.Date <= toDate.Value).ToList();

                var transactions = journalEntries.Select(je => new fatortak.Dtos.Transaction.TransactionDto
                {
                    Id = je.Id,
                    Date = je.Date.ToString("dd MMM yyyy"),
                    TransactionDate = je.Date,
                    Type = je.ReferenceType.ToString(),
                    Amount = je.Lines.Sum(l => l.Debit),
                    Direction = je.ReferenceType == JournalEntryReferenceType.Expense ? "Debit" : (je.ReferenceType == JournalEntryReferenceType.Invoice ? "Credit" : "Both"),
                    Description = je.Description,
                    Reference = je.EntryNumber,
                    ReferenceId = je.ReferenceId.ToString()
                }).ToList();

                // Income = revenue generated (+ direct cash income if any)
                var totalIncome = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId && jel.JournalEntry.ProjectId == projectId && jel.JournalEntry.IsPosted && jel.Account.AccountType == AccountType.Revenue)
                    .SumAsync(jel => jel.Credit - jel.Debit);

                // Expenses = expenses incurred (+ direct cash expenses)
                var totalExpenses = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId && jel.JournalEntry.ProjectId == projectId && jel.JournalEntry.IsPosted && jel.Account.AccountType == AccountType.Expense)
                    .SumAsync(jel => jel.Debit - jel.Credit);

                // Receivables = Current AR balance for this project
                var totalReceivables = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId && jel.JournalEntry.ProjectId == projectId && jel.JournalEntry.IsPosted && jel.Account.AccountCode.StartsWith("1200"))
                    .SumAsync(jel => jel.Debit - jel.Credit);

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
                // 1. Get Accounts Balances for Cash (1000) and Bank (1100)
                var accounts = await _context.Accounts
                    .Where(a => a.TenantId == _tenantId &&
                                (a.AccountCode.StartsWith("1000") || a.AccountCode.StartsWith("1100")))
                    .Select(a => new AccountBalanceDto
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Type = a.AccountType.ToString(),
                        Balance = 0, // Will calculate below
                        Currency = "USD" // Should be company currency
                    })
                    .ToListAsync();

                foreach (var account in accounts)
                {
                    account.Balance = await _context.JournalEntryLines
                        .Include(jel => jel.JournalEntry)
                        .Where(jel => jel.AccountId == account.Id && jel.JournalEntry.IsPosted)
                        .SumAsync(jel => jel.Debit - jel.Credit);
                }

                // 2. Get Recent Transactions for these accounts
                var transactions = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  (jel.Account.AccountCode.StartsWith("1000") || jel.Account.AccountCode.StartsWith("1100")))
                    .OrderByDescending(jel => jel.JournalEntry.Date)
                    .ThenByDescending(jel => jel.JournalEntry.CreatedAt)
                    .Take(50)
                    .Select(jel => new fatortak.Dtos.Transaction.TransactionDto
                    {
                        Id = jel.JournalEntryId,
                        Date = jel.JournalEntry.Date.ToString("dd MMM yyyy"),
                        TransactionDate = jel.JournalEntry.Date,
                        Type = jel.JournalEntry.ReferenceType.ToString(),
                        Amount = jel.Debit > 0 ? jel.Debit : jel.Credit,
                        Direction = jel.Debit > 0 ? "Debit" : "Credit",
                        Description = jel.JournalEntry.Description,
                        Reference = jel.JournalEntry.EntryNumber
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

        #region Employee Custody Report
        public async Task<ServiceResult<List<EmployeeCustodyReportDto>>> GetEmployeeCustodyReportAsync()
        {
            try
            {
                // Custody sub-accounts start with 1500
                var employeesQuery = _context.Accounts
                    .Where(a => a.TenantId == _tenantId && a.AccountCode.StartsWith("1500") && a.AccountCode != "1500");

                var employees = await employeesQuery.ToListAsync();
                var result = new List<EmployeeCustodyReportDto>();

                foreach (var emp in employees)
                {
                    var lines = await _context.JournalEntryLines
                        .Include(jel => jel.JournalEntry)
                        .Where(jel => jel.AccountId == emp.Id && jel.JournalEntry.IsPosted)
                        .ToListAsync();

                    var report = new EmployeeCustodyReportDto
                    {
                        EmployeeId = emp.Id,
                        EmployeeName = emp.Name,
                        CurrentBalance = lines.Sum(l => l.Debit - l.Credit),
                        TotalReceived = lines.Sum(l => l.Debit),
                        TotalSpent = lines.Sum(l => l.Credit),
                        Transactions = lines.OrderByDescending(l => l.JournalEntry.Date)
                            .Take(20)
                            .Select(l => new fatortak.Dtos.Transaction.TransactionDto
                            {
                                Id = l.JournalEntryId,
                                Date = l.JournalEntry.Date.ToString("dd MMM yyyy"),
                                TransactionDate = l.JournalEntry.Date,
                                Type = l.JournalEntry.ReferenceType.ToString(),
                                Amount = l.Debit > 0 ? l.Debit : l.Credit,
                                Direction = l.Debit > 0 ? "Debit" : "Credit",
                                Description = l.JournalEntry.Description
                            }).ToList()
                    };

                    result.Add(report);
                }

                return ServiceResult<List<EmployeeCustodyReportDto>>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee custody report");
                return ServiceResult<List<EmployeeCustodyReportDto>>.Failure("Failed to retrieve custody report");
            }
        }
        #endregion
    }
}
