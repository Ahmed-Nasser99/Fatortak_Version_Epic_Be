using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.AccountingPostingService
{
    /// <summary>
    /// Service for posting business transactions (invoices, expenses, payments) to accounting journal entries.
    /// Implements double-entry bookkeeping rules.
    /// </summary>
    public class AccountingPostingService : IAccountingPostingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountingPostingService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AccountingPostingService(
            ApplicationDbContext context,
            ILogger<AccountingPostingService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid TenantId => GetCurrentTenantId();
        private Guid? CurrentUserId => GetCurrentUserId();

        private Guid GetCurrentTenantId()
        {
            var tenant = _httpContextAccessor.HttpContext?.Items["CurrentTenant"] as Tenant;
            return tenant?.Id ?? Guid.Empty;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("UserId")?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        public async Task<bool> PostInvoiceAsync(Guid invoiceId)
        {
            var existingTransaction = _context.Database.CurrentTransaction;
            var transaction = existingTransaction == null ? await _context.Database.BeginTransactionAsync() : null;

            try
            {
                // Load invoice with related data
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);

                if (invoice == null)
                {
                    _logger.LogError("Invoice {InvoiceId} not found", invoiceId);
                    return false;
                }

                // Determine if this is a sales or purchase invoice
                var invoiceTypeStr = invoice.InvoiceType?.ToLower() ?? "";
                bool isSalesInvoice = invoiceTypeStr == InvoiceTypes.Sell.ToString().ToLower() ||
                                     invoiceTypeStr == "sales" ||
                                     invoiceTypeStr == "sale";

                var expectedRefType = isSalesInvoice ? JournalEntryReferenceType.Invoice : JournalEntryReferenceType.PurchaseInvoice;

                // Check if already posted
                var existingEntry = await _context.JournalEntries
                    .FirstOrDefaultAsync(je => je.TenantId == TenantId &&
                                               je.ReferenceType == expectedRefType &&
                                               je.ReferenceId == invoiceId &&
                                               je.IsPosted);

                if (existingEntry != null)
                {
                    _logger.LogWarning("Invoice {InvoiceId} has already been posted as {RefType}", invoiceId, expectedRefType);
                    return false;
                }

                // Get required accounts (these should be configured in Chart of Accounts)
                // For sales invoice on credit:
                //   Dr Accounts Receivable
                //   Cr Sales Revenue
                //   Cr VAT Payable (if VAT exists)

                // For purchase invoice on credit:
                //   Dr Expense/Inventory Account
                //   Dr VAT Input (if VAT exists)
                //   Cr Accounts Payable

                Account? accountsReceivableAccount = null;
                Account? accountsPayableAccount = null;

                if (isSalesInvoice)
                {
                    accountsReceivableAccount = invoice.Customer?.AccountId != null
                        ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == invoice.Customer.AccountId && a.TenantId == TenantId)
                        : await GetOrCreateSystemAccountAsync("1200", "Accounts Receivable", AccountType.Asset);

                    if (accountsReceivableAccount == null)
                    {
                        _logger.LogError("Accounts Receivable account not found for sales invoice posting");
                        await transaction.RollbackAsync();
                        return false;
                    }
                }
                else
                {
                    accountsPayableAccount = invoice.Customer?.AccountId != null
                        ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == invoice.Customer.AccountId && a.TenantId == TenantId)
                        : await GetOrCreateSystemAccountAsync("2100", "Accounts Payable", AccountType.Liability);

                    if (accountsPayableAccount == null)
                    {
                        _logger.LogError("Accounts Payable account not found for purchase invoice posting");
                        await transaction.RollbackAsync();
                        return false;
                    }
                }

                var salesRevenueAccount = await GetOrCreateSystemAccountAsync("4000", "Sales Revenue", AccountType.Revenue);
                var vatPayableAccount = await GetOrCreateSystemAccountAsync("2200", "VAT Payable", AccountType.Liability);
                var vatInputAccount = await GetOrCreateSystemAccountAsync("1300", "VAT Input", AccountType.Asset);

                if (isSalesInvoice && salesRevenueAccount == null)
                {
                    _logger.LogError("Sales Revenue account not found for sales invoice posting");
                    await transaction.RollbackAsync();
                    return false;
                }

                // Generate entry number
                var entryNumber = await GenerateEntryNumberAsync();

                var journalEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    EntryNumber = entryNumber,
                    Date = invoice.IssueDate.Date,
                    ReferenceType = isSalesInvoice ? JournalEntryReferenceType.Invoice : JournalEntryReferenceType.PurchaseInvoice,
                    ReferenceId = invoiceId,
                    ProjectId = invoice.ProjectId,
                    Description = $"Invoice {invoice.InvoiceNumber} - {invoice.Customer?.Name ?? "Customer"}",
                    IsPosted = true, // Auto-post when created from business transaction
                    PostedAt = DateTime.UtcNow,
                    PostedBy = CurrentUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.JournalEntries.Add(journalEntry);

                if (isSalesInvoice)
                {
                    // Sales Invoice (On Credit)
                    // Dr Accounts Receivable (Total)
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = accountsReceivableAccount.Id,
                        Debit = invoice.Total,
                        Credit = 0,
                        Description = $"Invoice {invoice.InvoiceNumber} - Receivable"
                    });

                    // Cr Sales Revenue (Subtotal)
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = salesRevenueAccount.Id,
                        Debit = 0,
                        Credit = invoice.Total,
                        Description = $"Invoice {invoice.InvoiceNumber} - Revenue"
                    });

                    // Cr VAT Payable (if VAT exists)
                    if (invoice.VatAmount > 0 && vatPayableAccount != null)
                    {
                        _context.JournalEntryLines.Add(new JournalEntryLine
                        {
                            Id = Guid.NewGuid(),
                            JournalEntryId = journalEntry.Id,
                            AccountId = vatPayableAccount.Id,
                            Debit = 0,
                            Credit = invoice.VatAmount,
                            Description = $"Invoice {invoice.InvoiceNumber} - VAT"
                        });
                    }
                }
                else
                {
                    // Purchase Invoice (On Credit)
                    // For simplicity, posting to a default expense account
                    // In production, this should map to specific expense/inventory accounts based on invoice items
                    var expenseAccount = await GetOrCreateSystemAccountAsync("5000", "General Expenses", AccountType.Expense);
                    if (expenseAccount == null)
                    {
                        _logger.LogError("Default expense account not found/could not be created for purchase invoice");
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Dr Expense Account (Subtotal)
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = expenseAccount.Id,
                        Debit = invoice.Subtotal,
                        Credit = 0,
                        Description = $"Purchase Invoice {invoice.InvoiceNumber} - Expense"
                    });

                    // Dr VAT Input (if VAT exists)
                    if (invoice.VatAmount > 0 && vatInputAccount != null)
                    {
                        _context.JournalEntryLines.Add(new JournalEntryLine
                        {
                            Id = Guid.NewGuid(),
                            JournalEntryId = journalEntry.Id,
                            AccountId = vatInputAccount.Id,
                            Debit = invoice.VatAmount,
                            Credit = 0,
                            Description = $"Purchase Invoice {invoice.InvoiceNumber} - VAT Input"
                        });
                    }

                    // Cr Accounts Payable (Total)
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = accountsPayableAccount.Id,
                        Debit = 0,
                        Credit = invoice.Total,
                        Description = $"Purchase Invoice {invoice.InvoiceNumber} - Payable"
                    });
                }

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                _logger.LogInformation("Successfully posted invoice {InvoiceId} as journal entry {EntryNumber}", invoiceId, entryNumber);
                return true;
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                _logger.LogError(ex, "Error posting invoice {InvoiceId}", invoiceId);
                return false;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }

        public async Task<bool> PostExpenseAsync(int expenseId)
        {
            var existingTransaction = _context.Database.CurrentTransaction;
            var transaction = existingTransaction == null ? await _context.Database.BeginTransactionAsync() : null;

            try
            {
                // Check if already posted
                // Convert expense ID (int) to string and use it as reference
                var expenseIdString = expenseId.ToString();
                var existingEntry = await _context.JournalEntries
                    .FirstOrDefaultAsync(je => je.TenantId == TenantId &&
                                               je.ReferenceType == JournalEntryReferenceType.Expense &&
                                               je.Description != null && je.Description.Contains($"Expense ID: {expenseId}") &&
                                               je.IsPosted);

                if (existingEntry != null)
                {
                    _logger.LogWarning("Expense {ExpenseId} has already been posted", expenseId);
                    return false;
                }

                // Load expense
                var expense = await _context.Expenses
                    .Include(e => e.Category)
                    .Include(e => e.PaymentAccount)
                    .FirstOrDefaultAsync(e => e.Id == expenseId && e.TenantId == TenantId);

                if (expense == null)
                {
                    _logger.LogError("Expense {ExpenseId} not found", expenseId);
                    return false;
                }

                // Get accounts
                // Expense:
                //   Dr Expense (Category Account)
                //   Cr Payment Source (Cash/Bank/Employee)

                Account? expenseAccount = null;

                // 1. Try to get account from Category
                if (expense.CategoryId.HasValue)
                {
                    var category = await _context.ExpenseCategories
                        .Include(c => c.Account)
                        .FirstOrDefaultAsync(c => c.Id == expense.CategoryId.Value);

                    if (category != null)
                    {
                        expenseAccount = category.Account;
                        expense.Category = category;
                    }
                }

                // Fallback to default if category account missing
                if (expenseAccount == null)
                {
                    expenseAccount = await GetAccountByCodeAsync("5000"); // Default Expense Account
                }

                // 2. Get payment account
                var cashAccount = expense.PaymentAccount;
                if (cashAccount == null && expense.PaymentAccountId.HasValue)
                {
                    cashAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == expense.PaymentAccountId.Value && a.TenantId == TenantId);
                }

                // Fallback to default cash if payment account missing
                if (cashAccount == null)
                {
                    cashAccount = await GetAccountByCodeAsync("1000"); // Default Cash Account
                }

                if (expenseAccount == null || cashAccount == null)
                {
                    _logger.LogError("Required accounts not found for expense posting");
                    await transaction.RollbackAsync();
                    return false;
                }

                // Generate entry number
                var entryNumber = await GenerateEntryNumberAsync();

                var journalEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    EntryNumber = entryNumber,
                    Date = expense.Date.ToDateTime(TimeOnly.MinValue).Date,
                    ReferenceType = JournalEntryReferenceType.Expense,
                    ReferenceId = null,
                    ProjectId = expense.ProjectId,
                    Description = $"Expense ID: {expenseId} - Category: {expense.Category?.Name ?? "General"} - Paid via: {cashAccount.Name}",
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    PostedBy = CurrentUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.JournalEntries.Add(journalEntry);

                // Dr Expense Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = expenseAccount.Id,
                    Debit = expense.Total,
                    Credit = 0,
                    Description = expense.Notes ?? $"Expense - {expense.Category?.Name ?? "General"}"
                });

                // Cr Cash Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = cashAccount.Id,
                    Debit = 0,
                    Credit = expense.Total,
                    Description = $"Payment for expense - {expense.Category?.Name ?? "General"}"
                });

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                _logger.LogInformation("Successfully posted expense {ExpenseId} as journal entry {EntryNumber}", expenseId, entryNumber);
                return true;
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                _logger.LogError(ex, "Error posting expense {ExpenseId}", expenseId);
                return false;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }

        public async Task<bool> PostPaymentAsync(Guid invoiceId, decimal amount, Guid? transactionId = null, Guid? paymentAccountId = null, string? paymentMethod = null)
        {
            var existingTransaction = _context.Database.CurrentTransaction;
            var transaction = existingTransaction == null ? await _context.Database.BeginTransactionAsync() : null;

            try
            {
                // Check if already posted
                var referenceId = transactionId ?? invoiceId;
                var existingEntry = await _context.JournalEntries
                    .FirstOrDefaultAsync(je => je.TenantId == TenantId &&
                                               je.ReferenceType == JournalEntryReferenceType.Payment &&
                                               je.ReferenceId == referenceId &&
                                               je.IsPosted);

                if (existingEntry != null)
                {
                    _logger.LogWarning("Payment (Ref: {ReferenceId}) for invoice {InvoiceId} has already been posted", referenceId, invoiceId);
                    return false;
                }

                // Load invoice
                var invoice = await _context.Invoices
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);

                if (invoice == null)
                {
                    _logger.LogError("Invoice {InvoiceId} not found", invoiceId);
                    return false;
                }

                // Determine if this is a sales or purchase invoice payment
                var invoiceTypeStr = invoice.InvoiceType?.ToLower() ?? "";
                bool isSalesInvoice = invoiceTypeStr == InvoiceTypes.Sell.ToString().ToLower() ||
                                     invoiceTypeStr == "sales" ||
                                     invoiceTypeStr == "sale";

                // Get accounts
                Account? accountsReceivableAccount = null;
                Account? accountsPayableAccount = null;
                Account? cashOrBankAccount = null;

                if (isSalesInvoice)
                {
                    // Customer Payment (Sales):
                    //   Dr Cash/Bank Account
                    //   Cr Accounts Receivable
                    accountsReceivableAccount = invoice.Customer?.AccountId != null
                        ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == invoice.Customer.AccountId && a.TenantId == TenantId)
                        : await GetOrCreateSystemAccountAsync("1200", "Accounts Receivable", AccountType.Asset);

                    if (accountsReceivableAccount == null)
                    {
                        _logger.LogError("Accounts Receivable account not found for payment posting");
                        await transaction.RollbackAsync();
                        return false;
                    }
                }
                else
                {
                    // Supplier Payment (Purchase):
                    //   Dr Accounts Payable
                    //   Cr Cash/Bank Account
                    accountsPayableAccount = invoice.Customer?.AccountId != null
                        ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == invoice.Customer.AccountId && a.TenantId == TenantId)
                        : await GetOrCreateSystemAccountAsync("2100", "Accounts Payable", AccountType.Liability);

                    if (accountsPayableAccount == null)
                    {
                        _logger.LogError("Accounts Payable account not found for payment posting");
                        await transaction.RollbackAsync();
                        return false;
                    }
                }

                // Use provided paymentAccountId or fallback to default Cash (1000)
                if (paymentAccountId.HasValue)
                {
                    cashOrBankAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == paymentAccountId.Value && a.TenantId == TenantId);
                }

                if (cashOrBankAccount == null)
                {
                    // Map "Cash" to account 1000, and everything else (BankTransfer, Cheque, etc.) to account 1100
                    if (!string.IsNullOrEmpty(paymentMethod) && paymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                    {
                        cashOrBankAccount = await GetOrCreateSystemAccountAsync("1000", "Cash", AccountType.Asset);
                    }
                    else
                    {
                        cashOrBankAccount = await GetOrCreateSystemAccountAsync("1100", "Bank Account", AccountType.Asset);
                    }

                    // Final backup fallback
                    if (cashOrBankAccount == null)
                    {
                        cashOrBankAccount = await GetOrCreateSystemAccountAsync("1000", "Cash", AccountType.Asset);
                    }
                }


                if ((isSalesInvoice && accountsReceivableAccount == null) ||
                    (!isSalesInvoice && accountsPayableAccount == null) ||
                    cashOrBankAccount == null)
                {
                    _logger.LogError("Required accounts not found for payment posting (Invoice: {InvoiceId}, Type: {Type})",
                        invoiceId, isSalesInvoice ? "Sales" : "Purchase");
                    await transaction.RollbackAsync();
                    return false;
                }

                // Generate entry number
                var entryNumber = await GenerateEntryNumberAsync();

                var journalEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    EntryNumber = entryNumber,
                    Date = DateTime.UtcNow.Date,
                    ReferenceType = JournalEntryReferenceType.Payment,
                    ReferenceId = referenceId,
                    ProjectId = invoice.ProjectId,
                    Description = $"{(isSalesInvoice ? "Payment received" : "Payment made")} for Invoice {invoice.InvoiceNumber} - {invoice.Customer?.Name ?? "Customer"}",
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    PostedBy = CurrentUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.JournalEntries.Add(journalEntry);

                _logger.LogInformation("Posting payment for Invoice {InvoiceId}. Amount: {Amount}, Method: {Method}, Account: {AccountName} ({AccountCode}), Sales: {IsSales}",
                    invoiceId, amount, paymentMethod, cashOrBankAccount.Name, cashOrBankAccount.AccountCode, isSalesInvoice);

                if (isSalesInvoice)
                {
                    _logger.LogInformation("Sales Payment: Debiting {CashAccount} ({CashCode}), Crediting {ArAccount} ({ArCode})",
                        cashOrBankAccount.Name, cashOrBankAccount.AccountCode, accountsReceivableAccount.Name, accountsReceivableAccount.AccountCode);
                    // Dr Cash/Bank Account
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = cashOrBankAccount.Id,
                        Debit = amount,
                        Credit = 0,
                        Description = $"Payment received for Invoice {invoice.InvoiceNumber}"
                    });

                    // Cr Accounts Receivable
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = accountsReceivableAccount.Id,
                        Debit = 0,
                        Credit = amount,
                        Description = $"Reduction of receivable for Invoice {invoice.InvoiceNumber}"
                    });
                }
                else
                {
                    _logger.LogInformation("Purchase Payment: Debiting {ApAccount} ({ApCode}), Crediting {CashAccount} ({CashCode})",
                        accountsPayableAccount.Name, accountsPayableAccount.AccountCode, cashOrBankAccount.Name, cashOrBankAccount.AccountCode);
                    // Dr Accounts Payable
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = accountsPayableAccount.Id,
                        Debit = amount,
                        Credit = 0,
                        Description = $"Payment made for Purchase Invoice {invoice.InvoiceNumber}"
                    });

                    // Cr Cash/Bank Account
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = cashOrBankAccount.Id,
                        Debit = 0,
                        Credit = amount,
                        Description = $"Reduction of cash/bank for Purchase Invoice {invoice.InvoiceNumber}"
                    });
                }

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                _logger.LogInformation("Successfully posted payment for invoice {InvoiceId} as journal entry {EntryNumber}", invoiceId, entryNumber);
                return true;
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                _logger.LogError(ex, "Error posting payment for invoice {InvoiceId}", invoiceId);
                return false;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }

        public async Task<bool> IsInvoicePostedAsync(Guid invoiceId)
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == TenantId);

            if (invoice == null) return false;

            var invoiceTypeStr = invoice.InvoiceType?.ToLower() ?? "";
            bool isSalesInvoice = invoiceTypeStr == InvoiceTypes.Sell.ToString().ToLower() ||
                                 invoiceTypeStr == "sales" ||
                                 invoiceTypeStr == "sale";

            var expectedRefType = isSalesInvoice ? JournalEntryReferenceType.Invoice : JournalEntryReferenceType.PurchaseInvoice;

            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(je => je.TenantId == TenantId &&
                                           je.ReferenceType == expectedRefType &&
                                           je.ReferenceId == invoiceId &&
                                           je.IsPosted);

            return entry != null;
        }

        public async Task<bool> IsExpensePostedAsync(int expenseId)
        {
            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(je => je.TenantId == TenantId &&
                                           je.ReferenceType == JournalEntryReferenceType.Expense &&
                                           je.Description != null && je.Description.Contains($"Expense ID: {expenseId}") &&
                                           je.IsPosted);

            return entry != null;
        }

        #region Helper Methods

        private async Task<Account?> GetAccountByCodeAsync(string accountCode)
        {
            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.AccountCode == accountCode && a.IsActive);
        }


        private async Task<Account?> GetOrCreateSystemAccountAsync(string code, string defaultName, AccountType type)
        {
            try
            {
                var account = await GetAccountByCodeAsync(code);
                if (account != null) return account;

                _logger.LogInformation("System account {Code} ({Name}) missing for tenant {TenantId}. Creating it.", code, defaultName, TenantId);

                account = new Account
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AccountCode = code,
                    Name = defaultName,
                    AccountType = type,
                    Level = 0,
                    IsActive = true,
                    IsPostable = true,
                    IsSystem = true,
                    Description = $"System account for {defaultName}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
                return account;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting or creating system account {Code}", code);
                return null;
            }
        }

        private async Task<string> GenerateEntryNumberAsync()
        {
            var lastEntry = await _context.JournalEntries
                .Where(je => je.TenantId == TenantId)
                .OrderByDescending(je => je.CreatedAt)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastEntry != null)
            {
                var parts = lastEntry.EntryNumber.Split('-');
                if (parts.Length > 1 && int.TryParse(parts[1], out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"JE-{nextNumber:D4}";
        }

        #endregion
    }
}

