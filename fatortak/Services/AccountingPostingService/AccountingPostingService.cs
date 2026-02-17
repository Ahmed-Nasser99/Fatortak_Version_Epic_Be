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
                // Check if already posted
                var existingEntry = await _context.JournalEntries
                    .FirstOrDefaultAsync(je => je.TenantId == TenantId &&
                                               je.ReferenceType == JournalEntryReferenceType.Invoice &&
                                               je.ReferenceId == invoiceId &&
                                               je.IsPosted);

                if (existingEntry != null)
                {
                    _logger.LogWarning("Invoice {InvoiceId} has already been posted", invoiceId);
                    return false;
                }

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
                bool isSalesInvoice = invoice.InvoiceType?.ToLower() == InvoiceTypes.Sell.ToString().ToLower();

                // Get required accounts (these should be configured in Chart of Accounts)
                // For sales invoice on credit:
                //   Dr Accounts Receivable
                //   Cr Sales Revenue
                //   Cr VAT Payable (if VAT exists)

                // For purchase invoice on credit:
                //   Dr Expense/Inventory Account
                //   Dr VAT Input (if VAT exists)
                //   Cr Accounts Payable

                var accountsReceivableAccount = await GetAccountByCodeAsync("1200"); // Accounts Receivable
                var accountsPayableAccount = await GetAccountByCodeAsync("2100"); // Accounts Payable
                var salesRevenueAccount = await GetAccountByCodeAsync("4000"); // Sales Revenue
                var vatPayableAccount = await GetAccountByCodeAsync("2200"); // VAT Payable
                var vatInputAccount = await GetAccountByCodeAsync("1300"); // VAT Input (for purchases)

                if (isSalesInvoice)
                {
                    if (accountsReceivableAccount == null || salesRevenueAccount == null)
                    {
                        _logger.LogError("Required accounts not found for sales invoice posting");
                        await transaction.RollbackAsync();
                        return false;
                    }
                }
                else
                {
                    if (accountsPayableAccount == null)
                    {
                        _logger.LogError("Required accounts not found for purchase invoice posting");
                        await transaction.RollbackAsync();
                        return false;
                    }
                }

                // Generate entry number
                var entryNumber = await GenerateEntryNumberAsync();

                var journalEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    EntryNumber = entryNumber,
                    Date = invoice.IssueDate.Date,
                    ReferenceType = JournalEntryReferenceType.Invoice,
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
                        Credit = invoice.Subtotal,
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
                    var expenseAccount = await GetAccountByCodeAsync("5000"); // Default Expense Account
                    if (expenseAccount == null)
                    {
                        _logger.LogError("Default expense account not found for purchase invoice");
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
                    .Include(e => e.Account)
                    .FirstOrDefaultAsync(e => e.Id == expenseId && e.TenantId == TenantId);

                if (expense == null)
                {
                    _logger.LogError("Expense {ExpenseId} not found", expenseId);
                    return false;
                }

                // Get accounts
                // Expense Paid Cash/Bank:
                //   Dr Expense Account
                //   Cr Cash/Bank Account

                var expenseAccount = await GetAccountByCodeAsync("5000"); // Default Expense Account
                
                // If the expense has a specific account selected, use it
                if (expense.AccountId.HasValue)
                {
                    var selectedAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == expense.AccountId.Value && a.TenantId == TenantId);
                    
                    if (selectedAccount != null)
                    {
                        expenseAccount = selectedAccount;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(expense.Category))
                {
                    // Try to get expense account by category if mapping exists
                    var categoryAccount = await GetAccountByNameAsync($"Expense - {expense.Category}");
                    if (categoryAccount != null)
                    {
                        expenseAccount = categoryAccount;
                    }
                }

                var cashAccount = await GetAccountByCodeAsync("1000"); // Cash Account

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
                    ReferenceId = null, // Expense uses int ID, store reference in description
                    ProjectId = expense.ProjectId,
                    Description = $"Expense ID: {expenseId} - {expenseAccount.Name} - {expense.Notes}",
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
                    Description = expense.Notes ?? $"Expense - {expense.Category}"
                });

                // Cr Cash Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = cashAccount.Id,
                    Debit = 0,
                    Credit = expense.Total,
                    Description = $"Payment for expense - {expense.Category}"
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

        public async Task<bool> PostPaymentAsync(Guid invoiceId, decimal amount, Guid? transactionId = null)
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

                // Customer Payment:
                //   Dr Cash/Bank Account
                //   Cr Accounts Receivable

                var accountsReceivableAccount = await GetAccountByCodeAsync("1200"); // Accounts Receivable
                var cashAccount = await GetAccountByCodeAsync("1000"); // Cash Account

                if (accountsReceivableAccount == null || cashAccount == null)
                {
                    _logger.LogError("Required accounts not found for payment posting");
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
                    Description = $"Payment for Invoice {invoice.InvoiceNumber} - {invoice.Customer?.Name ?? "Customer"}",
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    PostedBy = CurrentUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.JournalEntries.Add(journalEntry);

                // Dr Cash Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = cashAccount.Id,
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
            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(je => je.TenantId == TenantId &&
                                           je.ReferenceType == JournalEntryReferenceType.Invoice &&
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

        private async Task<Account?> GetAccountByNameAsync(string accountName)
        {
            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Name.Contains(accountName) && a.IsActive);
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

