using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.CustodyService
{
    /// <summary>
    /// Service for handling employee custody (advances) using Chart of Accounts.
    /// All custody operations are posted to accounting journal entries.
    /// </summary>
    public class CustodyService : ICustodyService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustodyService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustodyService(
            ApplicationDbContext context,
            ILogger<CustodyService> logger,
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

        public async Task<Guid> GetOrCreateEmployeeCustodyAccountAsync(Guid employeeId, string employeeName)
        {
            // Check if account already exists
            var accountCode = $"1500-{employeeId.ToString().Substring(0, 8)}"; // Format: 1500-{shortId}
            var existingAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.AccountCode == accountCode);

            if (existingAccount != null)
            {
                return existingAccount.Id;
            }

            // Get or create parent account for Employee Custody
            var parentAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.AccountCode == "1500");

            if (parentAccount == null)
            {
                // Create parent account for Employee Custody
                parentAccount = new Account
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AccountCode = "1500",
                    Name = "Employee Custody",
                    AccountType = AccountType.Asset,
                    Level = 0,
                    IsActive = true,
                    IsPostable = false, // Parent account, not postable
                    Description = "Employee advances and custody accounts",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };
                _context.Accounts.Add(parentAccount);
                await _context.SaveChangesAsync();
            }

            // Create employee-specific custody account
            var employeeAccount = new Account
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountCode = accountCode,
                Name = $"Custody - {employeeName}",
                AccountType = AccountType.Asset,
                ParentAccountId = parentAccount.Id,
                Level = 1,
                IsActive = true,
                IsPostable = true, // Leaf account, postable
                Description = $"Employee custody account for {employeeName}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = CurrentUserId
            };

            _context.Accounts.Add(employeeAccount);
            await _context.SaveChangesAsync();

            return employeeAccount.Id;
        }

        public async Task<bool> GiveCustodyAsync(Guid employeeId, decimal amount, Guid? sourceAccountId, string? description)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get employee
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == TenantId);

                if (employee == null)
                {
                    _logger.LogError("Employee {EmployeeId} not found", employeeId);
                    return false;
                }

                // Get or create employee custody account
                var custodyAccountId = await GetOrCreateEmployeeCustodyAccountAsync(employeeId, employee.FullName);

                // Get source account (Cash or Bank)
                var sourceAccount = sourceAccountId.HasValue
                    ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == sourceAccountId.Value && a.TenantId == TenantId)
                    : await GetAccountByCodeAsync("1000"); // Default to Cash

                if (sourceAccount == null)
                {
                    _logger.LogError("Source account not found for giving custody");
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
                    ReferenceType = JournalEntryReferenceType.Manual,
                    Description = description ?? $"Custody given to {employee.FullName}",
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    PostedBy = CurrentUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.JournalEntries.Add(journalEntry);

                // Dr Employee Custody Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = custodyAccountId,
                    Debit = amount,
                    Credit = 0,
                    Description = $"Custody given to {employee.FullName}"
                });

                // Cr Cash/Bank Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = sourceAccount.Id,
                    Debit = 0,
                    Credit = amount,
                    Description = $"Custody payment to {employee.FullName}"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully gave custody {Amount} to employee {EmployeeId}", amount, employeeId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error giving custody to employee {EmployeeId}", employeeId);
                return false;
            }
        }

        public async Task<bool> UseCustodyForExpenseAsync(int expenseId, Guid employeeId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get expense
                var expense = await _context.Expenses
                    .FirstOrDefaultAsync(e => e.Id == expenseId && e.TenantId == TenantId);

                if (expense == null)
                {
                    _logger.LogError("Expense {ExpenseId} not found", expenseId);
                    return false;
                }

                // Get employee
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == TenantId);

                if (employee == null)
                {
                    _logger.LogError("Employee {EmployeeId} not found", employeeId);
                    return false;
                }

                // Get or create employee custody account
                var custodyAccountId = await GetOrCreateEmployeeCustodyAccountAsync(employeeId, employee.FullName);

                // Get expense account
                var expenseAccount = await GetAccountByCodeAsync("5000"); // Default Expense Account
                if (!string.IsNullOrWhiteSpace(expense.Category))
                {
                    var categoryAccount = await GetAccountByNameAsync($"Expense - {expense.Category}");
                    if (categoryAccount != null)
                    {
                        expenseAccount = categoryAccount;
                    }
                }

                if (expenseAccount == null)
                {
                    _logger.LogError("Expense account not found");
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
                    Description = $"Expense ID: {expenseId} - Paid from custody - {employee.FullName}",
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

                // Cr Employee Custody Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = custodyAccountId,
                    Debit = 0,
                    Credit = expense.Total,
                    Description = $"Custody used for expense by {employee.FullName}"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully posted expense {ExpenseId} using custody for employee {EmployeeId}", expenseId, employeeId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error using custody for expense {ExpenseId}", expenseId);
                return false;
            }
        }

        public async Task<bool> ReturnCustodyAsync(Guid employeeId, decimal amount, Guid? destinationAccountId, string? description)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get employee
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == TenantId);

                if (employee == null)
                {
                    _logger.LogError("Employee {EmployeeId} not found", employeeId);
                    return false;
                }

                // Get employee custody account
                var custodyAccountId = await GetOrCreateEmployeeCustodyAccountAsync(employeeId, employee.FullName);

                // Get destination account (Cash or Bank)
                var destinationAccount = destinationAccountId.HasValue
                    ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == destinationAccountId.Value && a.TenantId == TenantId)
                    : await GetAccountByCodeAsync("1000"); // Default to Cash

                if (destinationAccount == null)
                {
                    _logger.LogError("Destination account not found for returning custody");
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
                    ReferenceType = JournalEntryReferenceType.Manual,
                    Description = description ?? $"Custody returned by {employee.FullName}",
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    PostedBy = CurrentUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.JournalEntries.Add(journalEntry);

                // Dr Cash/Bank Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = destinationAccount.Id,
                    Debit = amount,
                    Credit = 0,
                    Description = $"Custody returned by {employee.FullName}"
                });

                // Cr Employee Custody Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = custodyAccountId,
                    Debit = 0,
                    Credit = amount,
                    Description = $"Custody return from {employee.FullName}"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully returned custody {Amount} from employee {EmployeeId}", amount, employeeId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error returning custody from employee {EmployeeId}", employeeId);
                return false;
            }
        }

        public async Task<bool> ReplenishCustodyAsync(Guid employeeId, decimal amount, Guid? sourceAccountId, string? description)
        {
            // Replenish is same as Give - adds more money to custody
            return await GiveCustodyAsync(employeeId, amount, sourceAccountId, description ?? "Custody replenishment");
        }

        public async Task<decimal> GetEmployeeCustodyBalanceAsync(Guid employeeId)
        {
            try
            {
                // Get employee
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == TenantId);

                if (employee == null)
                {
                    return 0;
                }

                // Get employee custody account
                var custodyAccountId = await GetOrCreateEmployeeCustodyAccountAsync(employeeId, employee.FullName);

                // Get balance from accounting
                var query = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Where(jel => jel.AccountId == custodyAccountId && 
                                  jel.JournalEntry.TenantId == TenantId && 
                                  jel.JournalEntry.IsPosted);

                var debitTotal = await query.SumAsync(jel => jel.Debit);
                var creditTotal = await query.SumAsync(jel => jel.Credit);

                // Custody is an Asset account: Balance = Debit - Credit
                return debitTotal - creditTotal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting custody balance for employee {EmployeeId}", employeeId);
                return 0;
            }
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

