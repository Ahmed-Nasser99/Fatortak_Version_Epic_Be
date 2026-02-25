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

        public async Task<DashboardResponseDto> GetDashboardDataAsync(string period = "month", Guid? branchId = null, Guid? projectId = null)
        {
            var (startDate, endDate) = GetDateRange(period);
            var response = new DashboardResponseDto
            {
                Stats = await GetDashboardStatsAsync(startDate, endDate, branchId, projectId),
                RecentInvoices = await GetRecentInvoicesAsync(startDate, endDate, branchId, projectId),
                RecentTransactions = await GetRecentTransactionsAsync(startDate, endDate, branchId, projectId),
                MonthlyFinancials = await GetMonthlyFinancialsAsync(startDate, endDate, branchId, projectId)
            };

            return response;
        }

        // NEW METHOD: Get monthly financial data for chart
        private async Task<List<MonthlyFinancialDto>> GetMonthlyFinancialsAsync(DateTime startDate, DateTime endDate, Guid? branchId, Guid? projectId)
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

                // Revenue from accounts
                var revenue = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= monthStart &&
                                  jel.JournalEntry.Date <= monthEnd &&
                                  (!branchId.HasValue || jel.JournalEntry.ProjectId == null) && // Omitting branch filter for financial stats for now to fix build
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountType == AccountType.Revenue)
                    .SumAsync(jel => jel.Credit - jel.Debit);

                // Expenses from accounts
                var expenses = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                                  jel.JournalEntry.IsPosted &&
                                  jel.JournalEntry.Date >= monthStart &&
                                  jel.JournalEntry.Date <= monthEnd &&
                                  (!branchId.HasValue || ((jel.JournalEntry.ReferenceType == JournalEntryReferenceType.Invoice || jel.JournalEntry.ReferenceType == JournalEntryReferenceType.PurchaseInvoice) && _context.Invoices.Any(i => i.Id == jel.JournalEntry.ReferenceId && i.BranchId == branchId))) &&
                                  (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId) &&
                                  jel.Account.AccountType == AccountType.Expense)
                    .SumAsync(jel => jel.Debit - jel.Credit);

                result.Add(new MonthlyFinancialDto
                {
                    Month = monthStart.ToString("MMM"),
                    Year = monthStart.Year,
                    Revenue = Math.Max(0, revenue),
                    Expenses = Math.Max(0, expenses),
                    Profit = revenue - expenses
                });

                monthCursor = monthCursor.AddMonths(1);
            }

            return result;
        }


        private async Task<DashboardStatsDto> GetDashboardStatsAsync(DateTime startDate, DateTime endDate, Guid? branchId, Guid? projectId)
        {
            var stats = new DashboardStatsDto();

            // Financial stats based on account balances
            var linesQuery = _context.JournalEntryLines
                .Include(jel => jel.JournalEntry)
                .Include(jel => jel.Account)
                .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                              jel.JournalEntry.IsPosted &&
                              jel.JournalEntry.Date >= startDate &&
                              jel.JournalEntry.Date <= endDate &&
                              (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId));

            // ✅ Total Revenue (AccountType.Revenue)
            stats.TotalRevenue = await linesQuery
                .Where(jel => jel.Account.AccountType == AccountType.Revenue)
                .SumAsync(jel => jel.Credit - jel.Debit);

            // ✅ Total Expenses (AccountType.Expense)
            stats.TotalExpenses = await linesQuery
                .Where(jel => jel.Account.AccountType == AccountType.Expense)
                .SumAsync(jel => jel.Debit - jel.Credit);

            stats.NetIncome = stats.TotalRevenue - stats.TotalExpenses;
            // stats.CurrentBalance will be set after calculating TotalCashAvailable

            // ✅ Receivables & Payables (Cumulative up to endDate)
            var cumulativeQuery = _context.JournalEntryLines
                .Include(jel => jel.JournalEntry)
                .Include(jel => jel.Account)
                .Where(jel => jel.JournalEntry.TenantId == _tenantId &&
                              jel.JournalEntry.IsPosted &&
                              jel.JournalEntry.Date <= endDate &&
                              (!projectId.HasValue || jel.JournalEntry.ProjectId == projectId));

            // AR (Accounts Receivable - Code 1200)
            stats.TotalReceivables = await cumulativeQuery
                .Where(jel => jel.Account.AccountCode.StartsWith("1200"))
                .SumAsync(jel => jel.Debit - jel.Credit);

            // AP (Accounts Payable - Code 2100)
            stats.TotalPayables = await cumulativeQuery
                .Where(jel => jel.Account.AccountCode.StartsWith("2100"))
                .SumAsync(jel => jel.Credit - jel.Debit);

            stats.PendingAmount = stats.TotalReceivables;

            // ✅ Cash Available (Cash 1000)
            stats.TotalCashAvailable = await cumulativeQuery
                .Where(jel => jel.Account.AccountCode.StartsWith("1000"))
                .SumAsync(jel => jel.Debit - jel.Credit);

            stats.TotalBankAvailable = await cumulativeQuery
                .Where(jel => jel.Account.AccountCode.StartsWith("1100"))
                .SumAsync(jel => jel.Debit - jel.Credit);

            stats.CurrentBalance = stats.TotalCashAvailable + stats.TotalBankAvailable;

            // Non-financial counts (still document-based for now)
            stats.TotalInvoices = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.Status != InvoiceStatus.Cancelled.ToString() &&
                            i.Status != InvoiceStatus.Draft.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId) &&
                            (!projectId.HasValue || i.ProjectId == projectId))
                .CountAsync();

            stats.PaidInvoices = await _context.Invoices
                .Where(i => i.TenantId == _tenantId &&
                            i.Status == InvoiceStatus.Paid.ToString() &&
                            i.CreatedAt >= startDate &&
                            i.CreatedAt <= endDate &&
                            (!branchId.HasValue || i.BranchId == branchId) &&
                            (!projectId.HasValue || i.ProjectId == projectId))
                .CountAsync();

            stats.OverdueInvoices = await _context.Invoices
                .CountAsync(i => i.TenantId == _tenantId &&
                             i.CreatedAt >= startDate &&
                             i.CreatedAt <= endDate &&
                                 (i.InvoiceType == InvoiceTypes.Sell.ToString() || i.InvoiceType.ToLower() == "sales" || i.InvoiceType.ToLower() == "sale") &&
                                 i.Status == InvoiceStatus.Overdue.ToString() &&
                                 (!branchId.HasValue || i.BranchId == branchId) &&
                                 (!projectId.HasValue || i.ProjectId == projectId));

            stats.TotalCustomers = await _context.Customers
                .Where(c => c.TenantId == _tenantId && c.IsActive && !c.IsDeleted && !c.IsSupplier)
                .CountAsync();

            stats.TotalSuppliers = await _context.Customers
                .Where(c => c.TenantId == _tenantId && c.IsActive && !c.IsDeleted && c.IsSupplier)
                .CountAsync();

            stats.TotalItems = await _context.Items
                .Where(i => i.TenantId == _tenantId && !i.IsDeleted && (!branchId.HasValue || i.BranchId == branchId))
                .CountAsync();

            stats.ActiveItems = await _context.Items
                .Where(i => i.TenantId == _tenantId && i.IsActive && !i.IsDeleted && (!branchId.HasValue || i.BranchId == branchId))
                .CountAsync();

            // ✅ Stock Value
            stats.StockValue = await _context.Items
                .Where(i => i.TenantId == _tenantId &&
                           i.IsActive &&
                           !i.IsDeleted &&
                           i.Quantity.HasValue &&
                           i.PurchaseUnitPrice.HasValue &&
                           (!branchId.HasValue || i.BranchId == branchId))
                .SumAsync(i => i.Quantity.Value * i.PurchaseUnitPrice.Value);

            // ✅ Breakdown for tooltips
            // Revenue Paid = Collections (Credits to AR) + Direct Cash Sales (Debits to Cash from Revenue)
            var revenuePaid = await linesQuery
                .Where(jel => jel.Account.AccountCode.StartsWith("1200"))
                .SumAsync(jel => jel.Credit);

            stats.RevenueBreakdown = new RevenueBreakdown
            {
                Paid = revenuePaid,
                PartialPaid = 0,
                Pending = stats.TotalReceivables
            };

            // Expense Paid = Payments made to suppliers (Debits to AP) + Direct cash expenses
            var apPayments = await linesQuery
                .Where(jel => jel.Account.AccountCode.StartsWith("2100"))
                .SumAsync(jel => jel.Debit);
                
            var directExpenses = await linesQuery
                .Where(jel => jel.Account.AccountType == AccountType.Expense && jel.JournalEntry.ReferenceType == JournalEntryReferenceType.Expense)
                .SumAsync(jel => jel.Debit);
            
            var expensePaid = apPayments + directExpenses;

            stats.ExpenseBreakdown = new ExpenseBreakdown
            {
                Paid = expensePaid,
                PartialPaid = 0,
                Pending = stats.TotalPayables
            };

            stats.StartDate = startDate;
            stats.EndDate = endDate;

            return stats;
        }

        private async Task<List<RecentInvoiceDto>> GetRecentInvoicesAsync(DateTime startDate, DateTime endDate, Guid? branchId, Guid? projectId)
        {
            return await _context.Invoices
                .Include(i => i.Customer)
                .Where(i => i.TenantId == _tenantId && 
                            (!branchId.HasValue || i.BranchId == branchId) &&
                            (!projectId.HasValue || i.ProjectId == projectId))
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

        private async Task<List<TransactionDto>> GetRecentTransactionsAsync(DateTime startDate, DateTime endDate, Guid? branchId, Guid? projectId)
        {
            var journalEntries = await _context.JournalEntries
                .Include(je => je.Lines)
                .ThenInclude(l => l.Account)
                .Where(je => je.TenantId == _tenantId &&
                             je.IsPosted &&
                             je.Date >= startDate &&
                             je.Date <= endDate &&
                             (!branchId.HasValue || je.ProjectId == null) &&
                             (!projectId.HasValue || je.ProjectId == projectId))
                .OrderByDescending(je => je.Date)
                .ThenByDescending(je => je.CreatedAt)
                .Take(10)
                .ToListAsync();

            return journalEntries.Select(je =>
            {
                var totalDebit = je.Lines.Sum(l => l.Debit);
                
                // Determine transaction type and amount based on primary line
                string type = je.ReferenceType.ToString();
                decimal amount = totalDebit;
                decimal paid = 0;
                
                // If it's a payment, it's 100% paid by definition in accounting
                if (je.ReferenceType == JournalEntryReferenceType.Payment)
                {
                    paid = totalDebit;
                    type = "Payment";
                }
                else if (je.ReferenceType == JournalEntryReferenceType.Expense)
                {
                    paid = totalDebit;
                    type = "Expense";
                }
                else if (je.ReferenceType == JournalEntryReferenceType.Invoice)
                {
                    // For invoices, "paid" in this context is usually 0 unless it was a cash sale
                    // But for the list, we show the total invoice amount
                    type = "Sales";
                }
                else if (je.ReferenceType == JournalEntryReferenceType.PurchaseInvoice)
                {
                    type = "Purchase";
                }

                return new TransactionDto
                {
                    TransactionDateTime = je.CreatedAt,
                    Date = je.Date.ToString("dd MMM yyyy"),
                    Type = type,
                    Reference = je.Description ?? je.EntryNumber,
                    Amount = amount,
                    Paid = paid,
                    Remaining = amount - paid,
                    Status = paid >= amount ? "Paid" : (paid > 0 ? "Partial" : "Pending"),
                    CustomerId = null // Could be extracted from AR lines if needed
                };
            }).ToList();
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
            
            // Generate a safe timezone-padded "end of current local day", since IssueDate is often picked
            // locally (e.g., 2026-02-26) while UTC now is still the 25th, preventing premature exclusion.
            var endOfTodaySafe = now.Date.AddDays(2).AddTicks(-1);

            return period switch
            {
                "week" => (now.Date.AddDays(-7), endOfTodaySafe),
                "month" => (new DateTime(now.Year, now.Month, 1), endOfTodaySafe),
                "quarter" => (new DateTime(now.Year, now.Month, 1).AddMonths(-3), endOfTodaySafe),
                "year" => (new DateTime(now.Year, 1, 1), endOfTodaySafe),
                _ => (new DateTime(now.Year, now.Month, 1), endOfTodaySafe)
            };
        }
        #endregion
    }
}