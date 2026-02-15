using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Dashboard;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.DashboardService
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public DashboardService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<DashboardResponseDto> GetDashboardDataAsync(string period = "month", Guid? branchId = null)
        {
            var (startDate, endDate) = GetDateRange(period);
            var response = new DashboardResponseDto
            {
                Stats = await GetDashboardStatsAsync(startDate, endDate, branchId),
                RecentInvoices = await GetRecentInvoicesAsync(startDate, endDate, branchId),
                RecentTransactions = await GetRecentTransactionsAsync(startDate, endDate, branchId),
                MonthlyFinancials = await GetMonthlyFinancialsAsync(startDate, endDate, branchId) // NEW
            };

            return response;
        }

        // NEW METHOD: Get monthly financial data for chart
        private async Task<List<MonthlyFinancialDto>> GetMonthlyFinancialsAsync(DateTime startDate, DateTime endDate, Guid? branchId)
        {
            var result = new List<MonthlyFinancialDto>();

            // If no filters were passed, fallback to last 6 months
            bool noFilters = startDate == DateTime.MinValue && endDate == DateTime.MinValue;

            DateTime rangeStart, rangeEnd;

            if (noFilters)
            {
                var currentDate = DateTime.UtcNow;
                rangeStart = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(-5);
                rangeEnd = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(1).AddDays(-1);
            }
            else
            {
                rangeStart = new DateTime(startDate.Year, startDate.Month, 1);
                rangeEnd = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(1).AddDays(-1);
            }

            // Loop month-by-month inside the final range
            var monthCursor = rangeStart;

            while (monthCursor <= rangeEnd)
            {
                var monthStart = new DateTime(monthCursor.Year, monthCursor.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                // Revenue (Sell invoices)
                var revenue = await _context.Invoices
                    .Where(inv => inv.TenantId == _tenantId &&
                                  inv.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                  (inv.Status == InvoiceStatus.Paid.ToString() ||
                                   inv.Status == InvoiceStatus.PartialPaid.ToString()) &&
                                  inv.CreatedAt >= monthStart &&
                                  inv.CreatedAt <= monthEnd &&
                                  (!branchId.HasValue || inv.BranchId == branchId))
                    .SumAsync(inv => inv.Status == InvoiceStatus.PartialPaid.ToString() && inv.AmountPaid.HasValue
                        ? inv.AmountPaid.Value
                        : inv.Total);

                // Buy Expenses
                var buyExpenses = await _context.Invoices
                    .Where(inv => inv.TenantId == _tenantId &&
                                  inv.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                  (inv.Status == InvoiceStatus.Paid.ToString() ||
                                   inv.Status == InvoiceStatus.PartialPaid.ToString()) &&
                                  inv.CreatedAt >= monthStart &&
                                  inv.CreatedAt <= monthEnd &&
                                  (!branchId.HasValue || inv.BranchId == branchId))
                    .SumAsync(inv => inv.Status == InvoiceStatus.PartialPaid.ToString() && inv.AmountPaid.HasValue
                        ? inv.AmountPaid.Value
                        : inv.Total);

                // Other expenses
                var otherExpenses = await _context.Expenses
                    .Where(e => e.TenantId == _tenantId &&
                                e.CreatedAt >= monthStart &&
                                e.CreatedAt <= monthEnd &&
                                (!branchId.HasValue || e.BranchId == branchId))
                    .SumAsync(e => e.Total);

                var totalExpenses = buyExpenses + otherExpenses;

                // Add entry only if filters selected, OR if default 6 months logic
                result.Add(new MonthlyFinancialDto
                {
                    Month = monthStart.ToString("MMM"),
                    Year = monthStart.Year,
                    Revenue = revenue,
                    Expenses = totalExpenses,
                    Profit = revenue - totalExpenses
                });

                monthCursor = monthCursor.AddMonths(1);
            }

            return result;
        }


        private async Task<DashboardStatsDto> GetDashboardStatsAsync(DateTime startDate, DateTime endDate, Guid? branchId)
        {
            var stats = new DashboardStatsDto();

            // ✅ Total Revenue (Sell invoices - Paid + PartialPaid)
            stats.TotalRevenue = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                            i.Status != InvoiceStatus.Cancelled.ToString() &&
                            i.Status != InvoiceStatus.Draft.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            // ✅ Total Expenses (Buy invoices - Paid + PartialPaid)
            var buyExpenses = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                            i.Status != InvoiceStatus.Cancelled.ToString() &&
                            i.Status != InvoiceStatus.Draft.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            // ✅ Other Expenses & Salaries
            var otherExpenses = await _context.Expenses
                .Where(e => e.TenantId == _tenantId &&
                            e.CreatedAt >= startDate &&
                            e.CreatedAt <= endDate &&
                            (!branchId.HasValue || e.BranchId == branchId))
                .SumAsync(e => e.Total);

            var totalSalaries = await _context.Employees
                .Where(e => e.TenantId == _tenantId &&
                            e.CreatedAt >= startDate &&
                            e.CreatedAt <= endDate)
                .SumAsync(e => e.Salary);

            stats.TotalExpenses = buyExpenses + otherExpenses + totalSalaries.GetValueOrDefault();
            stats.NetIncome = stats.TotalRevenue - stats.TotalExpenses;
            stats.CurrentBalance = stats.TotalRevenue - stats.TotalExpenses;

            // ✅ Invoice counts
            stats.TotalInvoices = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.Status != InvoiceStatus.Cancelled.ToString() &&
                            i.Status != InvoiceStatus.Draft.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .CountAsync();

            stats.PaidInvoices = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.Status == InvoiceStatus.Paid.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .CountAsync();

            // ✅ Pending Amount (Receivables - Only Sell invoices pending)
            stats.TotalReceivables =
                // Pending sell invoices (not paid at all)
                await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.Status == InvoiceStatus.Pending.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                    .SumAsync(i => i.Total)
                +
                // PartialPaid sell invoices (remaining balance)
                await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                i.Status == InvoiceStatus.PartialPaid.ToString() &&
                                i.AmountPaid.HasValue &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                    .SumAsync(i => i.Total - i.AmountPaid.Value);

            // ✅ Total Payables (Buy invoices pending)
            stats.TotalPayables =
                // Pending buy invoices (not paid at all)
                await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                i.Status == InvoiceStatus.Pending.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                    .SumAsync(i => i.Total)
                +
                // PartialPaid buy invoices (remaining balance)
                await _context.Invoices
                    .Where(i => i.TenantId == _tenantId &&
                                i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                                i.Status == InvoiceStatus.PartialPaid.ToString() &&
                                i.AmountPaid.HasValue &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                    .SumAsync(i => i.Total - i.AmountPaid.Value);

            // Keep existing PendingAmount for backward compatibility
            stats.PendingAmount = stats.TotalReceivables;

            stats.OverdueInvoices = await _context.Invoices
                .CountAsync(i => i.TenantId == _tenantId &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                                 i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                                 i.Status == InvoiceStatus.Overdue.ToString() &&
                                 (!branchId.HasValue || i.BranchId == branchId));

            // ✅ Customer stats (Active Clients - only customers, not suppliers)
            stats.TotalCustomers = await _context.Customers
                .Where(c => c.TenantId == _tenantId &&
                            c.IsActive &&
                            !c.IsDeleted &&
                            !c.IsSupplier)
                .CountAsync();

            stats.TotalSuppliers = await _context.Customers
                .Where(c => c.TenantId == _tenantId &&
                            c.IsActive &&
                            !c.IsDeleted &&
                            c.IsSupplier)
                .CountAsync();

            // ✅ Item stats (for Stock Value calculation)
            stats.TotalItems = await _context.Items
                .Where(i => i.TenantId == _tenantId && !i.IsDeleted && (!branchId.HasValue || i.BranchId == branchId))
                .CountAsync();

            stats.ActiveItems = await _context.Items
                .Where(i => i.TenantId == _tenantId && i.IsActive && !i.IsDeleted && (!branchId.HasValue || i.BranchId == branchId))
                .CountAsync();

            // ✅ Stock Value (Sum of all active items with quantity and unit price)
            stats.StockValue = await _context.Items
                .Where(i => i.TenantId == _tenantId &&
                           i.IsActive &&
                           !i.IsDeleted &&
                           i.Quantity.HasValue &&
                           i.PurchaseUnitPrice.HasValue &&
                           (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Quantity.Value * i.PurchaseUnitPrice.Value);

            // ✅ Total Cash Available (Revenue - Expenses - Payables)
            // This represents liquid cash after accounting for what's owed
            stats.TotalCashAvailable = stats.TotalRevenue - stats.TotalExpenses;

            // Ensure cash available is not negative (for display purposes)
            if (stats.TotalCashAvailable < 0)
                stats.TotalCashAvailable = 0;

            // ✅ Revenue Breakdown for tooltips
            var revenuePaid = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                            i.Status == InvoiceStatus.Paid.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            var revenuePartialPaid = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                            i.Status == InvoiceStatus.PartialPaid.ToString() &&
                            i.AmountPaid.HasValue &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            var revenuePending = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Sell.ToString() &&
                            i.Status == InvoiceStatus.Pending.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            stats.RevenueBreakdown = new RevenueBreakdown
            {
                Paid = revenuePaid,
                PartialPaid = revenuePartialPaid,
                Pending = revenuePending
            };

            // ✅ Expense Breakdown for tooltips
            var expensePaid = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                            i.Status == InvoiceStatus.Paid.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            var expensePartialPaid = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                            i.Status == InvoiceStatus.PartialPaid.ToString() &&
                            i.AmountPaid.HasValue &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            var expensePending = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.InvoiceType == InvoiceTypes.Buy.ToString() &&
                            i.Status == InvoiceStatus.Pending.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Total);

            stats.ExpenseBreakdown = new ExpenseBreakdown
            {
                Paid = expensePaid + otherExpenses + totalSalaries.GetValueOrDefault(),
                PartialPaid = expensePartialPaid,
                Pending = expensePending
            };

            // Add date range
            stats.StartDate = startDate;
            stats.EndDate = endDate;

            return stats;
        }

        private async Task<List<RecentInvoiceDto>> GetRecentInvoicesAsync(DateTime startDate, DateTime endDate, Guid? branchId)
        {
            return await _context.Invoices
                .Include(i => i.Customer)
                .Where(i => i.TenantId == _tenantId && (!branchId.HasValue || i.BranchId == branchId))
                .OrderByDescending(i => i.CreatedAt)
                .Take(5)
                .Select(i => new RecentInvoiceDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    CustomerName = i.Customer.Name,
                    Status = i.Status,
                    Total = i.Total,
                    IssueDate = i.IssueDate,
                    DueDate = i.DueDate,
                    CreatedAt = i.CreatedAt,
                    InvoiceType = i.InvoiceType
                })
                .ToListAsync();
        }

        private async Task<List<TransactionDto>> GetRecentTransactionsAsync(DateTime startDate, DateTime endDate, Guid? branchId)
        {
            return await _context.Transactions
                .Where(t => t.TenantId == _tenantId &&
                            t.TransactionDate >= startDate &&
                            t.TransactionDate <= endDate &&
                            (!branchId.HasValue || t.BranchId == branchId))
                .OrderByDescending(t => t.TransactionDate)
                .Take(5)
                .Select(t => new TransactionDto
                {
                    TransactionDateTime = t.TransactionDate,
                    Date = t.TransactionDate.ToString("dd MMM yyyy"),
                    Type = t.Type,
                    Reference = t.Description ?? t.ReferenceType,
                    Amount = t.Amount,
                    Paid = t.Amount,
                    Remaining = 0,
                    Status = "Paid",
                    CustomerId = null
                })
                .ToListAsync();
        }

        private async Task<List<TransactionDto>> GetRecentTransactionsAsync_Old(DateTime startDate, DateTime endDate)
        {
            var transactions = new List<TransactionDto>();

            // ---------------------------------------------------------
            // 1) INVOICES
            // ---------------------------------------------------------
            var invoiceQuery = _context.Invoices
                .Include(i => i.Customer)
                .Where(i => i.TenantId == _tenantId &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate)
                .AsQueryable();

            var invoices = await invoiceQuery
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new TransactionDto
                {
                    TransactionDateTime = i.CreatedAt,
                    Date = i.CreatedAt.ToString("dd MMM yyyy"),
                    Type = i.InvoiceType == InvoiceTypes.Sell.ToString() ? "Sales" : "Purchase",
                    Reference = i.InvoiceNumber,
                    Amount = i.Total,
                    Paid = i.Status == InvoiceStatus.Paid.ToString()
                                ? i.Total
                                : (i.AmountPaid ?? 0),
                    Remaining = i.Status != InvoiceStatus.Paid.ToString()
                                ? i.Total : 0,
                    CustomerId = i.CustomerId,
                    Status = i.Status
                })
                .ToListAsync();


            // ---------------------------------------------------------
            // 2) INSTALLMENTS
            // ---------------------------------------------------------
            var installmentQuery = _context.Installments
                .Include(i => i.Invoice)
                .Where(i => i.TenantId == _tenantId)
                .Where(i => i.PaidAt.HasValue &&
                            i.PaidAt >= startDate &&
                            i.PaidAt <= endDate)
                .AsQueryable();


            var installments = await installmentQuery
                .OrderByDescending(i => i.PaidAt)
                .Select(i => new TransactionDto
                {
                    TransactionDateTime = i.PaidAt.Value,
                    Date = i.DueDate.ToString("dd MMM yyyy"),
                    Type = "Installment",
                    Reference = i.Invoice.InvoiceNumber,
                    Amount = i.Amount,
                    Paid = i.Status == InstallmentStatus.Paid.ToString() ? i.Amount : 0,
                    Remaining = i.Status == InstallmentStatus.Paid.ToString() ? 0 : i.Amount,
                    CustomerId = i.Invoice.CustomerId,
                    Status = i.Status
                })
                .ToListAsync();


            // ---------------------------------------------------------
            // 3) EXPENSES
            // ---------------------------------------------------------
            var expenseQuery = _context.Expenses
                .Where(e => e.TenantId == _tenantId &&
                            e.CreatedAt >= startDate &&
                            e.CreatedAt <= endDate)
                .AsQueryable();

            var expenses = await expenseQuery
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new TransactionDto
                {
                    TransactionDateTime = e.CreatedAt,
                    Date = e.CreatedAt.ToString("dd MMM yyyy"),
                    Type = "Expense",
                    Reference = string.IsNullOrEmpty(e.Notes) ? "EXP" :
                                e.Notes.Substring(0, Math.Min(10, e.Notes.Length)),
                    Amount = e.Total,
                    Paid = e.Total,
                    Remaining = 0,
                    Status = InvoiceStatus.Paid.ToString()
                })
                .ToListAsync();


            // ---------------------------------------------------------
            // Combine all transactions
            // ---------------------------------------------------------
            transactions.AddRange(invoices);
            transactions.AddRange(installments);
            transactions.AddRange(expenses);

            return transactions
                .OrderByDescending(t => t.TransactionDateTime)
                .Take(5)
                .ToList();
        }
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
                _ => (new DateTime(now.Year, now.Month, 1), now)
            };
        }
        #endregion
    }
}