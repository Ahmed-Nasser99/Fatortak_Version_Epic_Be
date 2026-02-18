using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Accounting;
using fatortak.Entities;
using fatortak.Services.AccountingService;
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
        private readonly IAccountingService _accountingService;

        public CustodyService(
            ApplicationDbContext context,
            ILogger<CustodyService> logger,
            IHttpContextAccessor httpContextAccessor,
            IAccountingService accountingService)
        {
            _context = context;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _accountingService = accountingService;
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

        public async Task<bool> GiveCustodyByAccountAsync(Guid accountId, decimal amount, Guid? sourceAccountId, string? description)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);
                if (account == null)
                {
                    _logger.LogError("Custody account {AccountId} not found", accountId);
                    return false;
                }

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
                    Description = description ?? $"Custody given to {account.Name}",
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
                    AccountId = account.Id,
                    Debit = amount,
                    Credit = 0,
                    Description = description ?? $"Custody given to {account.Name}"
                });

                // Cr Cash/Bank Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = sourceAccount.Id,
                    Debit = 0,
                    Credit = amount,
                    Description = description ?? $"Custody payment to {account.Name}"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully gave custody {Amount} to account {AccountId}", amount, accountId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error giving custody to account {AccountId}", accountId);
                return false;
            }
        }


        public async Task<bool> ReturnCustodyByAccountAsync(Guid accountId, decimal amount, Guid? destinationAccountId, string? description)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);
                if (account == null)
                {
                    _logger.LogError("Custody account {AccountId} not found", accountId);
                    return false;
                }

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
                    Description = description ?? $"Custody returned by {account.Name}",
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
                    Description = description ?? $"Custody returned to {destinationAccount.Name}"
                });

                // Cr Employee Custody Account
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = account.Id,
                    Debit = 0,
                    Credit = amount,
                    Description = description ?? $"Custody return from {account.Name}"
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully returned custody {Amount} from account {AccountId}", amount, accountId);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error returning custody from account {AccountId}", accountId);
                return false;
            }
        }


        public async Task<bool> ReplenishCustodyByAccountAsync(Guid accountId, decimal amount, Guid? sourceAccountId, string? description)
        {
            return await GiveCustodyByAccountAsync(accountId, amount, sourceAccountId, description ?? "Custody replenishment");
        }


        private async Task<decimal> GetAccountBalanceAsync(Guid accountId)
        {
            // Get balance from accounting
            var query = _context.JournalEntryLines
                .Include(jel => jel.JournalEntry)
                .Where(jel => jel.AccountId == accountId && 
                              jel.JournalEntry.TenantId == TenantId && 
                              jel.JournalEntry.IsPosted);

            var debitTotal = await query.SumAsync(jel => jel.Debit);
            var creditTotal = await query.SumAsync(jel => jel.Credit);

            // Custody is an Asset account: Balance = Debit - Credit
            return debitTotal - creditTotal;
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

        public async Task<AccountDto> CreateCustodyAccountAsync(string name, string? description)
        {
            try
            {
                // Find the "Employee Custody" parent account (code 1500)
                var parentAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.AccountCode == "1500" && a.TenantId == TenantId);

                if (parentAccount == null)
                {
                    throw new Exception("Employee Custody parent account (1500) not found. Please ensure it is seeded.");
                }

                var result = await _accountingService.GetOrCreateAccountForEntityAsync(
                    name,
                    Common.Enum.AccountType.Asset,
                    parentAccount.Id
                );

                if (!result.Success)
                {
                    throw new Exception(result.ErrorMessage);
                }

                // Update description if provided
                if (!string.IsNullOrWhiteSpace(description))
                {
                    await _accountingService.UpdateAccountAsync(result.Data.Id, new AccountUpdateDto { Description = description });
                    result.Data.Description = description;
                }

                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating custody account for {Name}", name);
                throw;
            }
        }
        #endregion
    }
}

