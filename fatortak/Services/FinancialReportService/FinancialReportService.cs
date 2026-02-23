using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Reports;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Helpers;
using fatortak.Services.AccountingService;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace fatortak.Services.FinancialReportService
{
    public class FinancialReportService : IFinancialReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FinancialReportService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FinancialReportService(
            ApplicationDbContext context,
            ILogger<FinancialReportService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Tenant? CurrentTenant => _httpContextAccessor.HttpContext?.Items["CurrentTenant"] as fatortak.Entities.Tenant;
        private Guid TenantId => CurrentTenant?.Id ?? Guid.Empty;

        #region Financial Reports

        public async Task<ServiceResult<TrialBalanceDto>> GetTrialBalanceAsync(DateTime? asOfDate = null)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<TrialBalanceDto>.Failure("Tenant context not found");
                var date = asOfDate ?? DateTime.UtcNow;
                var query = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted && jel.JournalEntry.Date <= date);

                var accountBalances = await query
                    .GroupBy(jel => new { jel.AccountId, jel.Account.AccountCode, jel.Account.Name, jel.Account.AccountType })
                    .Select(g => new
                    {
                        g.Key.AccountId,
                        g.Key.AccountCode,
                        g.Key.Name,
                        g.Key.AccountType,
                        DebitTotal = g.Sum(jel => jel.Debit),
                        CreditTotal = g.Sum(jel => jel.Credit)
                    })
                    .ToListAsync();

                var items = accountBalances.Select(ab => new TrialBalanceItemDto
                {
                    AccountId = ab.AccountId,
                    AccountCode = ab.AccountCode,
                    AccountName = ab.Name,
                    ClosingDebit = ab.DebitTotal,
                    ClosingCredit = ab.CreditTotal,
                    NetBalance = CalculateBalance(ab.AccountType, ab.DebitTotal, ab.CreditTotal)
                }).OrderBy(i => i.AccountCode).ToList();

                var totalDebit = items.Sum(i => i.ClosingDebit);
                var totalCredit = items.Sum(i => i.ClosingCredit);

                return ServiceResult<TrialBalanceDto>.SuccessResult(new TrialBalanceDto
                {
                    Title = "Trial Balance",
                    ToDate = date,
                    Items = items,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    IsBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Trial Balance");
                return ServiceResult<TrialBalanceDto>.Failure($"Error generating Trial Balance: {ex.Message}");
            }
        }

        public async Task<ServiceResult<LedgerDto>> GetAccountLedgerAsync(Guid accountId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<LedgerDto>.Failure("Tenant context not found");
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);

                if (account == null) return ServiceResult<LedgerDto>.Failure("Account not found");

                decimal openingBalance = 0;
                if (fromDate.HasValue)
                {
                    var openingQuery = _context.JournalEntryLines
                        .Include(jel => jel.JournalEntry)
                        .Where(jel => jel.AccountId == accountId && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted && jel.JournalEntry.Date < fromDate.Value);

                    var openingDebit = await openingQuery.SumAsync(jel => jel.Debit);
                    var openingCredit = await openingQuery.SumAsync(jel => jel.Credit);
                    openingBalance = CalculateBalance(account.AccountType, openingDebit, openingCredit);
                }

                var entriesQuery = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Where(jel => jel.AccountId == accountId && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted);

                if (fromDate.HasValue) entriesQuery = entriesQuery.Where(jel => jel.JournalEntry.Date >= fromDate.Value);
                if (toDate.HasValue) entriesQuery = entriesQuery.Where(jel => jel.JournalEntry.Date <= toDate.Value);

                var entries = await entriesQuery
                    .OrderBy(jel => jel.JournalEntry.Date)
                    .ThenBy(jel => jel.JournalEntry.CreatedAt)
                    .ToListAsync();

                var ledgerEntries = new List<LedgerEntryDto>();
                decimal runningBalance = openingBalance;

                foreach (var entry in entries)
                {
                    runningBalance += CalculateBalanceChange(account.AccountType, entry.Debit, entry.Credit);
                    ledgerEntries.Add(new LedgerEntryDto
                    {
                        JournalEntryId = entry.JournalEntryId,
                        EntryNumber = entry.JournalEntry.EntryNumber,
                        Date = entry.JournalEntry.Date,
                        Description = entry.Description ?? entry.JournalEntry.Description,
                        Debit = entry.Debit,
                        Credit = entry.Credit,
                        RunningBalance = runningBalance,
                        Reference = entry.Reference
                    });
                }

                return ServiceResult<LedgerDto>.SuccessResult(new LedgerDto
                {
                    Title = "Account Ledger",
                    AccountId = account.Id,
                    AccountCode = account.AccountCode,
                    AccountName = account.Name,
                    FromDate = fromDate,
                    ToDate = toDate,
                    OpeningBalance = openingBalance,
                    ClosingBalance = runningBalance,
                    Entries = ledgerEntries
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Ledger");
                return ServiceResult<LedgerDto>.Failure($"Error generating Ledger: {ex.Message}");
            }
        }

        public async Task<ServiceResult<IncomeStatementDto>> GetIncomeStatementAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<IncomeStatementDto>.Failure("Tenant context not found");
                var revenueAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Revenue && a.IsActive)
                    .ToListAsync();

                var expenseAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Expense && a.IsActive)
                    .ToListAsync();

                var revenueItems = new List<IncomeStatementItemDto>();
                var expenseItems = new List<IncomeStatementItemDto>();

                foreach (var account in revenueAccounts)
                {
                    var balance = await GetAccountBalanceForPeriod(account.Id, fromDate, toDate);
                    if (balance != 0)
                    {
                        revenueItems.Add(new IncomeStatementItemDto
                        {
                            AccountId = account.Id,
                            AccountCode = account.AccountCode,
                            AccountName = account.Name,
                            Amount = balance
                        });
                    }
                }

                foreach (var account in expenseAccounts)
                {
                    var balance = await GetAccountBalanceForPeriod(account.Id, fromDate, toDate);
                    if (balance != 0)
                    {
                        expenseItems.Add(new IncomeStatementItemDto
                        {
                            AccountId = account.Id,
                            AccountCode = account.AccountCode,
                            AccountName = account.Name,
                            Amount = balance
                        });
                    }
                }

                var totalRevenue = revenueItems.Sum(i => i.Amount);
                var totalExpenses = expenseItems.Sum(i => i.Amount);

                return ServiceResult<IncomeStatementDto>.SuccessResult(new IncomeStatementDto
                {
                    Title = "Income Statement",
                    FromDate = fromDate,
                    ToDate = toDate,
                    RevenueItems = revenueItems.OrderBy(i => i.AccountCode).ToList(),
                    TotalRevenue = totalRevenue,
                    ExpenseItems = expenseItems.OrderBy(i => i.AccountCode).ToList(),
                    TotalExpenses = totalExpenses,
                    NetIncome = totalRevenue - totalExpenses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Income Statement");
                return ServiceResult<IncomeStatementDto>.Failure($"Error generating Income Statement: {ex.Message}");
            }
        }

        public async Task<ServiceResult<BalanceSheetDto>> GetBalanceSheetAsync(DateTime asOfDate)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<BalanceSheetDto>.Failure("Tenant context not found");
                var assetAccounts = await _context.Accounts.Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Asset && a.IsActive).ToListAsync();
                var liabilityAccounts = await _context.Accounts.Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Liability && a.IsActive).ToListAsync();
                var equityAccounts = await _context.Accounts.Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Equity && a.IsActive).ToListAsync();

                var assets = new List<BalanceSheetItemDto>();
                foreach (var account in assetAccounts)
                {
                    var balance = await GetAccountBalanceAtDate(account.Id, asOfDate);
                    if (balance != 0) assets.Add(new BalanceSheetItemDto { AccountId = account.Id, AccountCode = account.AccountCode, AccountName = account.Name, Balance = balance });
                }

                var liabilities = new List<BalanceSheetItemDto>();
                foreach (var account in liabilityAccounts)
                {
                    var balance = await GetAccountBalanceAtDate(account.Id, asOfDate);
                    if (balance != 0) liabilities.Add(new BalanceSheetItemDto { AccountId = account.Id, AccountCode = account.AccountCode, AccountName = account.Name, Balance = balance });
                }

                var equity = new List<BalanceSheetItemDto>();
                foreach (var account in equityAccounts)
                {
                    var balance = await GetAccountBalanceAtDate(account.Id, asOfDate);
                    if (balance != 0) equity.Add(new BalanceSheetItemDto { AccountId = account.Id, AccountCode = account.AccountCode, AccountName = account.Name, Balance = balance });
                }

                var totalAssets = assets.Sum(a => a.Balance);
                var totalLiabilities = liabilities.Sum(l => l.Balance);
                var totalEquity = equity.Sum(e => e.Balance);

                return ServiceResult<BalanceSheetDto>.SuccessResult(new BalanceSheetDto
                {
                    Title = "Balance Sheet",
                    ToDate = asOfDate,
                    Assets = assets.OrderBy(a => a.AccountCode).ToList(),
                    TotalAssets = totalAssets,
                    Liabilities = liabilities.OrderBy(l => l.AccountCode).ToList(),
                    TotalLiabilities = totalLiabilities,
                    Equity = equity.OrderBy(e => e.AccountCode).ToList(),
                    TotalEquity = totalEquity,
                    IsBalanced = Math.Abs(totalAssets - (totalLiabilities + totalEquity)) < 0.01m
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Balance Sheet");
                return ServiceResult<BalanceSheetDto>.Failure($"Error generating Balance Sheet: {ex.Message}");
            }
        }

        #endregion

        #region Cash Flow Report

        public async Task<ServiceResult<CashFlowReportDto>> GetCashFlowReportAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<CashFlowReportDto>.Failure("Tenant context not found");
                var cashAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Asset && a.IsActive && 
                               (a.Name.Contains("Cash") || a.Name.Contains("Bank") || a.AccountCode.StartsWith("110")))
                    .ToListAsync();
                
                var cashAccountIds = cashAccounts.Select(a => a.Id).ToList();

                var startingCash = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => cashAccountIds.Contains(jel.AccountId) && 
                                 jel.JournalEntry.TenantId == TenantId && 
                                 jel.JournalEntry.IsPosted && 
                                 jel.JournalEntry.Date < fromDate)
                    .ToListAsync();
                
                var startingTotal = 0m;
                var groupedStarting = startingCash.GroupBy(jel => jel.Account.AccountType);
                foreach(var group in groupedStarting)
                {
                    var debit = group.Sum(jel => jel.Debit);
                    var credit = group.Sum(jel => jel.Credit);
                    startingTotal += CalculateBalance(group.Key, debit, credit);
                }

                var cashEntries = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => cashAccountIds.Contains(jel.AccountId) && 
                                 jel.JournalEntry.TenantId == TenantId && 
                                 jel.JournalEntry.IsPosted && 
                                 jel.JournalEntry.Date >= fromDate && 
                                 jel.JournalEntry.Date <= toDate)
                    .ToListAsync();

                var netCashChange = cashEntries.Sum(e => e.Debit - e.Credit);

                return ServiceResult<CashFlowReportDto>.SuccessResult(new CashFlowReportDto
                {
                    Title = "Cash Flow Report",
                    FromDate = fromDate,
                    ToDate = toDate,
                    StartingCash = startingTotal,
                    NetCashChange = netCashChange,
                    EndingCash = startingTotal + netCashChange,
                    Sections = new List<CashFlowSectionDto>
                    {
                        new CashFlowSectionDto 
                        { 
                            SectionName = "Net Cash Movement", 
                            Items = new List<CashFlowItemDto> { new CashFlowItemDto { Name = "Net Cash In/Out", Amount = netCashChange } },
                            Total = netCashChange
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Cash Flow Report");
                return ServiceResult<CashFlowReportDto>.Failure($"Error generating Cash Flow Report: {ex.Message}");
            }
        }

        #endregion

        #region Aging Reports

        public async Task<ServiceResult<AgingReportDto>> GetARAgingReportAsync(DateTime asOfDate)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<AgingReportDto>.Failure("Tenant context not found");
                var invoices = await _context.Invoices
                    .Include(i => i.Customer)
                    .Where(i => i.TenantId == TenantId && i.IssueDate <= asOfDate && i.InvoiceType == InvoiceTypes.Sell.ToString() && i.Total - (i.AmountPaid ?? 0) > 0)
                    .ToListAsync();

                var agingItems = invoices.GroupBy(i => new { i.CustomerId, i.Customer.Name })
                    .Select(g => 
                    {
                        var item = new AgingItemDto { EntityId = g.Key.CustomerId ?? Guid.Empty, EntityName = g.Key.Name };
                        foreach(var inv in g)
                        {
                            var daysOld = (asOfDate - inv.IssueDate).Days;
                            var balance = inv.Total - (inv.AmountPaid ?? 0);
                            if (daysOld <= 30) item.Balance0To30 += balance;
                            else if (daysOld <= 60) item.Balance31To60 += balance;
                            else if (daysOld <= 90) item.Balance61To90 += balance;
                            else item.Balance91Plus += balance;
                            item.TotalBalance += balance;
                        }
                        return item;
                    }).ToList();

                return ServiceResult<AgingReportDto>.SuccessResult(new AgingReportDto
                {
                    Title = "Accounts Receivable Aging",
                    ToDate = asOfDate,
                    Items = agingItems,
                    Total0To30 = agingItems.Sum(i => i.Balance0To30),
                    Total31To60 = agingItems.Sum(i => i.Balance31To60),
                    Total61To90 = agingItems.Sum(i => i.Balance61To90),
                    Total91Plus = agingItems.Sum(i => i.Balance91Plus),
                    GrandTotal = agingItems.Sum(i => i.TotalBalance)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AR Aging");
                return ServiceResult<AgingReportDto>.Failure($"Error generating AR Aging: {ex.Message}");
            }
        }

        public async Task<ServiceResult<AgingReportDto>> GetAPAgingReportAsync(DateTime asOfDate)
        {
             try
            {
                if (TenantId == Guid.Empty) return ServiceResult<AgingReportDto>.Failure("Tenant context not found");
                var invoices = await _context.Invoices
                    .Include(i => i.Customer)
                    .Where(i => i.TenantId == TenantId && i.IssueDate <= asOfDate && i.InvoiceType == InvoiceTypes.Buy.ToString() && i.Total - (i.AmountPaid ?? 0) > 0)
                    .ToListAsync();

                var agingItems = invoices.GroupBy(i => new { i.CustomerId, i.Customer.Name })
                    .Select(g => 
                    {
                        var item = new AgingItemDto { EntityId = g.Key.CustomerId ?? Guid.Empty, EntityName = g.Key.Name };
                        foreach(var inv in g)
                        {
                            var daysOld = (asOfDate - inv.IssueDate).Days;
                            var balance = inv.Total - (inv.AmountPaid ?? 0);
                            if (daysOld <= 30) item.Balance0To30 += balance;
                            else if (daysOld <= 60) item.Balance31To60 += balance;
                            else if (daysOld <= 90) item.Balance61To90 += balance;
                            else item.Balance91Plus += balance;
                            item.TotalBalance += balance;
                        }
                        return item;
                    }).ToList();

                return ServiceResult<AgingReportDto>.SuccessResult(new AgingReportDto
                {
                    Title = "Accounts Payable Aging",
                    ToDate = asOfDate,
                    Items = agingItems,
                    Total0To30 = agingItems.Sum(i => i.Balance0To30),
                    Total31To60 = agingItems.Sum(i => i.Balance31To60),
                    Total61To90 = agingItems.Sum(i => i.Balance61To90),
                    Total91Plus = agingItems.Sum(i => i.Balance91Plus),
                    GrandTotal = agingItems.Sum(i => i.TotalBalance)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AP Aging");
                return ServiceResult<AgingReportDto>.Failure($"Error generating AP Aging: {ex.Message}");
            }
        }

        #endregion

        #region Statements & Sales

        public async Task<ServiceResult<StatementReportDto>> GetCustomerStatementAsync(Guid customerId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<StatementReportDto>.Failure("Tenant context not found");
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == TenantId);
                if (customer == null) return ServiceResult<StatementReportDto>.Failure("Customer not found");

                var entries = new List<StatementEntryDto>();
                
                // Get all invoices for this customer
                var invoices = await _context.Invoices
                    .Where(i => i.CustomerId == customerId && i.TenantId == TenantId && i.IssueDate >= fromDate && i.IssueDate <= toDate && i.InvoiceType == InvoiceTypes.Sell.ToString())
                    .ToListAsync();
                
                foreach(var inv in invoices)
                {
                    entries.Add(new StatementEntryDto
                    {
                        Date = inv.IssueDate,
                        Description = $"Invoice {inv.InvoiceNumber}",
                        ReferenceNumber = inv.InvoiceNumber,
                        Debit = inv.Total,
                        Credit = 0
                    });
                }

                // Get all journal entries linked to this customer's invoices (for payments)
                var invoiceIds = invoices.Select(i => i.Id).ToList();
                var payments = await _context.JournalEntries
                    .Include(je => je.Lines)
                    .Where(je => je.TenantId == TenantId && je.IsPosted && je.ReferenceType == JournalEntryReferenceType.Payment && je.Date >= fromDate && je.Date <= toDate)
                    .ToListAsync();
                
                // Note: Simplified logic to match description. Ideally we'd link payment to invoice specifically.
                // For now, let's look for payments that mention the customer in description or link to their invoices.
                foreach(var je in payments)
                {
                    if (je.Description != null && (je.Description.Contains(customer.Name) || invoiceIds.Any(id => je.Description.Contains(id.ToString()))))
                    {
                        var arLine = je.Lines.FirstOrDefault(l => l.Credit > 0); // AR reduction is a credit
                        if (arLine != null)
                        {
                            entries.Add(new StatementEntryDto
                            {
                                Date = je.Date,
                                Description = je.Description,
                                ReferenceNumber = je.EntryNumber,
                                Debit = 0,
                                Credit = arLine.Credit
                            });
                        }
                    }
                }

                // Calculate running balance
                var openingBalance = 0m; // Simplified: should fetch balance before fromDate
                var runningBalance = openingBalance;
                var sortedEntries = entries.OrderBy(e => e.Date).ToList();
                foreach(var entry in sortedEntries)
                {
                    runningBalance += (entry.Debit - entry.Credit);
                    entry.RunningBalance = runningBalance;
                }

                return ServiceResult<StatementReportDto>.SuccessResult(new StatementReportDto
                {
                    Title = "Customer Statement",
                    EntityId = customerId,
                    EntityName = customer.Name,
                    FromDate = fromDate,
                    ToDate = toDate,
                    OpeningBalance = openingBalance,
                    ClosingBalance = runningBalance,
                    Entries = sortedEntries
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Customer Statement");
                return ServiceResult<StatementReportDto>.Failure($"Error generating Customer Statement: {ex.Message}");
            }
        }

        public async Task<ServiceResult<StatementReportDto>> GetVendorStatementAsync(Guid vendorId, DateTime fromDate, DateTime toDate)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<StatementReportDto>.Failure("Tenant context not found");
                var supplier = await _context.Customers.FirstOrDefaultAsync(s => s.Id == vendorId && s.TenantId == TenantId && s.IsSupplier);
                if (supplier == null) return ServiceResult<StatementReportDto>.Failure("Vendor not found");

                var entries = new List<StatementEntryDto>();
                
                // Get all purchase invoices
                var invoices = await _context.Invoices
                    .Where(i => i.CustomerId == vendorId && i.TenantId == TenantId && i.IssueDate >= fromDate && i.IssueDate <= toDate && i.InvoiceType == InvoiceTypes.Buy.ToString())
                    .ToListAsync();
                
                foreach(var inv in invoices)
                {
                    entries.Add(new StatementEntryDto
                    {
                        Date = inv.IssueDate,
                        Description = $"Purchase Invoice {inv.InvoiceNumber}",
                        ReferenceNumber = inv.InvoiceNumber,
                        Debit = 0,
                        Credit = inv.Total // Payable is a credit balance, but in statement we use Debit/Credit columns
                    });
                }

                var paymentEntries = await _context.JournalEntries
                    .Include(je => je.Lines)
                    .Where(je => je.TenantId == TenantId && je.IsPosted && je.ReferenceType == JournalEntryReferenceType.Payment && je.Date >= fromDate && je.Date <= toDate)
                    .ToListAsync();
                
                foreach(var je in paymentEntries)
                {
                    if (je.Description != null && je.Description.Contains(supplier.Name))
                    {
                        var apLine = je.Lines.FirstOrDefault(l => l.Debit > 0); // AP reduction is a debit
                        if (apLine != null)
                        {
                            entries.Add(new StatementEntryDto
                            {
                                Date = je.Date,
                                Description = je.Description,
                                ReferenceNumber = je.EntryNumber,
                                Debit = apLine.Debit,
                                Credit = 0
                            });
                        }
                    }
                }

                var openingBalance = 0m;
                var runningBalance = openingBalance;
                var sortedEntries = entries.OrderBy(e => e.Date).ToList();
                foreach(var entry in sortedEntries)
                {
                    // For Vendor, balance = Credit (Payable) - Debit (Payment)
                    runningBalance += (entry.Credit - entry.Debit);
                    entry.RunningBalance = runningBalance;
                }

                return ServiceResult<StatementReportDto>.SuccessResult(new StatementReportDto
                {
                    Title = "Vendor Statement",
                    EntityId = vendorId,
                    EntityName = supplier.Name,
                    FromDate = fromDate,
                    ToDate = toDate,
                    OpeningBalance = openingBalance,
                    ClosingBalance = runningBalance,
                    Entries = sortedEntries
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Vendor Statement");
                return ServiceResult<StatementReportDto>.Failure($"Error generating Vendor Statement: {ex.Message}");
            }
        }

        public async Task<ServiceResult<SalesReportDto>> GetSalesReportAsync(DateTime fromDate, DateTime toDate, Guid? customerId = null, Guid? projectId = null)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<SalesReportDto>.Failure("Tenant context not found");
                var query = _context.Invoices.Include(i => i.Customer).Where(i => i.TenantId == TenantId && i.IssueDate >= fromDate && i.IssueDate <= toDate && i.InvoiceType == InvoiceTypes.Sell.ToString());
                if (customerId.HasValue) query = query.Where(i => i.CustomerId == customerId.Value);
                if (projectId.HasValue) query = query.Where(i => i.ProjectId == projectId.Value);

                var invoices = await query.ToListAsync();
                var items = invoices.GroupBy(i => new { i.CustomerId, i.Customer.Name })
                    .Select(g => new SalesSummaryItemDto
                    {
                        CustomerId = g.Key.CustomerId ?? Guid.Empty,
                        CustomerName = g.Key.Name,
                        InvoiceCount = g.Count(),
                        TotalAmount = g.Sum(i => i.Total),
                        TotalPaid = g.Sum(i => i.AmountPaid ?? 0),
                        TotalRemaining = g.Sum(i => i.Total - (i.AmountPaid ?? 0))
                    }).ToList();

                return ServiceResult<SalesReportDto>.SuccessResult(new SalesReportDto
                {
                    Title = "Sales Report",
                    FromDate = fromDate,
                    ToDate = toDate,
                    Items = items,
                    TotalAmount = invoices.Sum(i => i.Total),
                    NetSales = invoices.Sum(i => i.Total - i.VatAmount)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Sales Report");
                return ServiceResult<SalesReportDto>.Failure($"Error generating Sales Report: {ex.Message}");
            }
        }

        #endregion

        #region Project Reports

        public async Task<ServiceResult<ProjectProfitabilityDto>> GetProjectProfitabilityReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<ProjectProfitabilityDto>.Failure("Tenant context not found");
                var projectsQuery = _context.Projects.Include(p => p.Customer).Where(p => p.TenantId == TenantId);
                var projects = await projectsQuery.ToListAsync();

                var items = new List<ProjectProfitabilityItemDto>();
                foreach(var p in projects)
                {
                    var revenue = await _context.Invoices.Where(i => i.ProjectId == p.Id && i.TenantId == TenantId && i.InvoiceType == InvoiceTypes.Sell.ToString()).SumAsync(i => i.Total);
                    var expenses = await _context.Expenses.Where(e => e.ProjectId == p.Id && e.TenantId == TenantId).SumAsync(e => e.Total);
                    
                    items.Add(new ProjectProfitabilityItemDto
                    {
                        ProjectId = p.Id,
                        ProjectName = p.Name,
                        ClientName = p.Customer?.Name,
                        ContractValue = p.ContractValue,
                        TotalRevenue = revenue,
                        TotalExpenses = expenses
                    });
                }

                return ServiceResult<ProjectProfitabilityDto>.SuccessResult(new ProjectProfitabilityDto
                {
                    Title = "Project Profitability Report",
                    FromDate = fromDate,
                    ToDate = toDate,
                    Projects = items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Project Profitability");
                return ServiceResult<ProjectProfitabilityDto>.Failure($"Error generating Project Profitability: {ex.Message}");
            }
        }

        public async Task<ServiceResult<ProjectCostBreakdownDto>> GetProjectCostBreakdownAsync(Guid projectId)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<ProjectCostBreakdownDto>.Failure("Tenant context not found");
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);
                if (project == null) return ServiceResult<ProjectCostBreakdownDto>.Failure("Project not found");

                var expenses = await _context.Expenses
                    .Include(e => e.Category)
                    .Where(e => e.ProjectId == projectId && e.TenantId == TenantId)
                    .ToListAsync();

                var categories = expenses.GroupBy(e => e.Category?.Name ?? "Uncategorized")
                    .Select(g => new ProjectCostCategoryDto
                    {
                        CategoryName = g.Key,
                        Amount = g.Sum(e => e.Total)
                    }).ToList();

                var totalCost = categories.Sum(c => c.Amount);
                foreach(var c in categories) c.Percentage = totalCost != 0 ? (c.Amount / totalCost) * 100 : 0;

                return ServiceResult<ProjectCostBreakdownDto>.SuccessResult(new ProjectCostBreakdownDto
                {
                    Title = "Project Cost Breakdown",
                    ProjectId = projectId,
                    ProjectName = project.Name,
                    Categories = categories,
                    TotalCost = totalCost
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Project Cost Breakdown");
                return ServiceResult<ProjectCostBreakdownDto>.Failure($"Error generating Project Cost Breakdown: {ex.Message}");
            }
        }

        public async Task<ServiceResult<MovementReportDto>> GetMovementReportAsync(Guid? accountId, DateTime fromDate, DateTime toDate, Guid? projectId = null, Guid? branchId = null)
        {
            try
            {
                if (TenantId == Guid.Empty) return ServiceResult<MovementReportDto>.Failure("Tenant context not found");

                var result = new MovementReportDto
                {
                    Title = "Cash & Bank Movement Report",
                    FromDate = fromDate,
                    ToDate = toDate,
                    Mode = accountId.HasValue ? "SingleAccount" : "AllCashAndBank"
                };

                List<Account> accountsToProcess;
                if (accountId.HasValue)
                {
                    var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId.Value && a.TenantId == TenantId);
                    if (account == null) return ServiceResult<MovementReportDto>.Failure("Account not found");
                    accountsToProcess = new List<Account> { account };
                }
                else
                {
                    // Broaden search to include existing accounts that are marked as Assets but named Cash/Bank/Custody
                    accountsToProcess = await _context.Accounts
                        .Where(a => a.TenantId == TenantId && 
                                   (a.AccountType == AccountType.Cash || 
                                    a.AccountType == AccountType.Bank || 
                                    a.AccountCode.StartsWith("10") || 
                                    a.AccountCode.StartsWith("11") || 
                                    a.AccountCode.StartsWith("15") || 
                                    a.Name.Contains("Cash") || 
                                    a.Name.Contains("Bank") || 
                                    a.Name.Contains("عهده") ||
                                    a.Name.Contains("صندوق")) && 
                                   a.IsActive && a.IsPostable)
                        .ToListAsync();
                }

                foreach (var account in accountsToProcess)
                {
                    var accountMovement = new AccountMovementDto
                    {
                        AccountId = account.Id,
                        AccountName = account.Name
                    };

                    // Opening Balance calculation
                    var openingQuery = _context.JournalEntryLines
                        .Include(jel => jel.JournalEntry)
                        .Where(jel => jel.AccountId == account.Id && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted && jel.JournalEntry.Date < fromDate);

                    if (projectId.HasValue) openingQuery = openingQuery.Where(jel => jel.JournalEntry.ProjectId == projectId.Value);

                    var openingDebit = await openingQuery.SumAsync(jel => (decimal?)jel.Debit) ?? 0;
                    var openingCredit = await openingQuery.SumAsync(jel => (decimal?)jel.Credit) ?? 0;
                    accountMovement.OpeningBalance = CalculateBalance(account.AccountType, openingDebit, openingCredit);

                    // Movements in period
                    var movementQuery = _context.JournalEntryLines
                        .Include(jel => jel.JournalEntry)
                        .ThenInclude(je => je.Project)
                        .Where(jel => jel.AccountId == account.Id && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted && jel.JournalEntry.Date >= fromDate && jel.JournalEntry.Date <= toDate);

                    if (projectId.HasValue) movementQuery = movementQuery.Where(jel => jel.JournalEntry.ProjectId == projectId.Value);

                    var entries = await movementQuery
                        .OrderBy(jel => jel.JournalEntry.Date)
                        .ThenBy(jel => jel.JournalEntry.CreatedAt)
                        .ToListAsync();

                    decimal runningBalance = accountMovement.OpeningBalance;
                    decimal totalIncome = 0;
                    decimal totalExpense = 0;

                    foreach (var entry in entries)
                    {
                        // Income/Expense mapping: Debit is Income, Credit is Expense for these asset-like accounts
                        var income = entry.Debit;
                        var expense = entry.Credit;
                        
                        totalIncome += income;
                        totalExpense += expense;
                        
                        runningBalance += CalculateBalanceChange(account.AccountType, entry.Debit, entry.Credit);

                        accountMovement.Movements.Add(new MovementEntryDto
                        {
                            JournalEntryId = entry.JournalEntryId,
                            EntryNumber = entry.JournalEntry.EntryNumber,
                            Date = entry.JournalEntry.Date,
                            Description = entry.Description ?? entry.JournalEntry.Description,
                            Reference = entry.Reference,
                            Debit = entry.Debit,
                            Credit = entry.Credit,
                            Income = income,
                            Expense = expense,
                            RunningBalance = runningBalance,
                            ProjectName = entry.JournalEntry.Project?.Name,
                            AccountName = account.Name // Track source account
                        });
                    }

                    accountMovement.TotalIncome = totalIncome;
                    accountMovement.TotalExpense = totalExpense;
                    accountMovement.ClosingBalance = runningBalance;

                    result.Accounts.Add(accountMovement);
                }

                // Fill global consolidated data if not single mode
                if (result.Mode == "AllCashAndBank")
                {
                    result.OpeningBalance = result.Accounts.Sum(a => a.OpeningBalance);
                    result.TotalIncome = result.Accounts.Sum(a => a.TotalIncome);
                    result.TotalExpense = result.Accounts.Sum(a => a.TotalExpense);
                    result.ClosingBalance = result.Accounts.Sum(a => a.ClosingBalance);
                    
                    // Flatten and sort movements for the consolidated view
                    result.Movements = result.Accounts
                        .SelectMany(a => a.Movements)
                        .OrderBy(m => m.Date)
                        .ThenBy(m => m.EntryNumber)
                        .ToList();
                }
                else if (result.Accounts.Any())
                {
                    var acc = result.Accounts.First();
                    result.AccountName = acc.AccountName;
                    result.OpeningBalance = acc.OpeningBalance;
                    result.TotalIncome = acc.TotalIncome;
                    result.TotalExpense = acc.TotalExpense;
                    result.ClosingBalance = acc.ClosingBalance;
                    result.Movements = acc.Movements;
                }

                return ServiceResult<MovementReportDto>.SuccessResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Movement Report");
                return ServiceResult<MovementReportDto>.Failure($"Error generating Movement Report: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private async Task<decimal> GetAccountBalanceAtDate(Guid accountId, DateTime asOfDate)
        {
            var query = _context.JournalEntryLines
                .Include(jel => jel.JournalEntry)
                .Where(jel => jel.AccountId == accountId && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted && jel.JournalEntry.Date <= asOfDate);

            var account = await _context.Accounts.FindAsync(accountId);
            var debit = await query.SumAsync(jel => jel.Debit);
            var credit = await query.SumAsync(jel => jel.Credit);

            return CalculateBalance(account.AccountType, debit, credit);
        }

        private async Task<decimal> GetAccountBalanceForPeriod(Guid accountId, DateTime fromDate, DateTime toDate)
        {
            var query = _context.JournalEntryLines
                .Include(jel => jel.JournalEntry)
                .Where(jel => jel.AccountId == accountId && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted && jel.JournalEntry.Date >= fromDate && jel.JournalEntry.Date <= toDate);

            var account = await _context.Accounts.FindAsync(accountId);
            var debit = await query.SumAsync(jel => jel.Debit);
            var credit = await query.SumAsync(jel => jel.Credit);

            return CalculateBalance(account.AccountType, debit, credit);
        }

        private decimal CalculateBalance(AccountType accountType, decimal debit, decimal credit)
        {
            return accountType switch
            {
                AccountType.Asset or AccountType.Expense or AccountType.Cash or AccountType.Bank or AccountType.Other => debit - credit,
                AccountType.Liability or AccountType.Equity or AccountType.Revenue => credit - debit,
                _ => 0
            };
        }

        private decimal CalculateBalanceChange(AccountType accountType, decimal debit, decimal credit)
        {
            return accountType switch
            {
                AccountType.Asset or AccountType.Expense or AccountType.Cash or AccountType.Bank or AccountType.Other => debit - credit,
                AccountType.Liability or AccountType.Equity or AccountType.Revenue => credit - debit,
                _ => 0
            };
        }

        #endregion
    }
}
