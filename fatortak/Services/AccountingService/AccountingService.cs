using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Accounting;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace fatortak.Services.AccountingService
{
    /// <summary>
    /// Service implementation for accounting operations and general ledger queries.
    /// Handles Chart of Accounts management and financial reporting.
    /// </summary>
    public class AccountingService : IAccountingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountingService> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AccountingService(
            ApplicationDbContext context,
            ILogger<AccountingService> logger,
            IWebHostEnvironment hostingEnvironment,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
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

        #region Account Management

        public async Task<ServiceResult<AccountDto>> CreateAccountAsync(AccountCreateDto dto)
        {
            try
            {
                // Validate account code uniqueness
                var existingAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.AccountCode == dto.AccountCode);

                if (existingAccount != null)
                {
                    return ServiceResult<AccountDto>.Failure($"Account code '{dto.AccountCode}' already exists for this tenant");
                }

                // Validate parent account if provided
                int level = 0;
                Account? parentAccount = null;
                if (dto.ParentAccountId.HasValue)
                {
                    parentAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == dto.ParentAccountId.Value && a.TenantId == TenantId);

                    if (parentAccount == null)
                    {
                        return ServiceResult<AccountDto>.Failure("Parent account not found");
                    }

                    if (!parentAccount.IsActive)
                    {
                        return ServiceResult<AccountDto>.Failure("Parent account is not active");
                    }

                    level = parentAccount.Level + 1;

                    // If parent is postable, make it non-postable
                    if (parentAccount.IsPostable)
                    {
                        parentAccount.IsPostable = false;
                        parentAccount.UpdatedAt = DateTime.UtcNow;
                        parentAccount.UpdatedBy = CurrentUserId;
                    }
                }

                var account = new Account
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AccountCode = dto.AccountCode,
                    Name = dto.Name,
                    AccountType = dto.AccountType,
                    ParentAccountId = dto.ParentAccountId,
                    Level = level,
                    IsActive = dto.IsActive,
                    IsPostable = dto.IsPostable, // Parent accounts automatically get their IsPostable set to false when a child is added
                    Description = dto.Description,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                return ServiceResult<AccountDto>.SuccessResult(MapToDto(account));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account");
                return ServiceResult<AccountDto>.Failure($"Error creating account: {ex.Message}");
            }
        }

        public async Task<ServiceResult<AccountDto>> GetAccountAsync(Guid accountId)
        {
            try
            {
                var account = await _context.Accounts
                    .Include(a => a.ParentAccount)
                    .Include(a => a.ChildAccounts)
                    .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);

                if (account == null)
                {
                    return ServiceResult<AccountDto>.Failure("Account not found");
                }

                var balanceResult = await GetAccountBalanceAsync(accountId);
                var balance = balanceResult.Success ? balanceResult.Data.Balance : 0;

                return ServiceResult<AccountDto>.SuccessResult(MapToDto(account, balance));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account");
                return ServiceResult<AccountDto>.Failure($"Error getting account: {ex.Message}");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<AccountDto>>> GetAccountsAsync(AccountFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.Accounts
                    .Include(a => a.ParentAccount)
                    .Where(a => a.TenantId == TenantId)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(filter.AccountCode))
                {
                    query = query.Where(a => a.AccountCode.Contains(filter.AccountCode));
                }

                if (!string.IsNullOrWhiteSpace(filter.Name))
                {
                    query = query.Where(a => a.Name.Contains(filter.Name));
                }

                if (filter.AccountType.HasValue)
                {
                    query = query.Where(a => a.AccountType == filter.AccountType.Value);
                }

                if (filter.ParentAccountId.HasValue)
                {
                    query = query.Where(a => a.ParentAccountId == filter.ParentAccountId.Value);
                }
                else if (filter.ParentAccountId == null && filter.IncludeInactive != true)
                {
                    // If not filtering by parent, optionally filter root accounts
                }

                if (filter.IsActive.HasValue)
                {
                    query = query.Where(a => a.IsActive == filter.IsActive.Value);
                }
                else if (filter.IncludeInactive != true)
                {
                    query = query.Where(a => a.IsActive);
                }

                if (filter.IsPostable.HasValue)
                {
                    query = query.Where(a => a.IsPostable == filter.IsPostable.Value);
                }

                var totalCount = await query.CountAsync();

                var accounts = await query
                    .OrderBy(a => a.AccountCode)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                // Fetch balances in bulk
                var accountIds = accounts.Select(a => a.Id).ToList();
                var balances = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Where(jel => accountIds.Contains(jel.AccountId) && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted)
                    .GroupBy(jel => jel.AccountId)
                    .Select(g => new
                    {
                        AccountId = g.Key,
                        Debit = g.Sum(jel => jel.Debit),
                        Credit = g.Sum(jel => jel.Credit)
                    })
                    .ToDictionaryAsync(x => x.AccountId, x => new { x.Debit, x.Credit });

                var accountDtos = accounts.Select(a =>
                {
                    var bal = balances.ContainsKey(a.Id) ? balances[a.Id] : new { Debit = 0m, Credit = 0m };
                    var balance = CalculateBalance(a.AccountType, bal.Debit, bal.Credit);
                    return MapToDto(a, balance);
                }).ToList();

                return ServiceResult<PagedResponseDto<AccountDto>>.SuccessResult(new PagedResponseDto<AccountDto>
                {
                    Data = accountDtos,
                    TotalCount = totalCount,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting accounts");
                return ServiceResult<PagedResponseDto<AccountDto>>.Failure($"Error getting accounts: {ex.Message}");
            }
        }

        public async Task<ServiceResult<AccountDto>> UpdateAccountAsync(Guid accountId, AccountUpdateDto dto)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);

                if (account == null)
                {
                    return ServiceResult<AccountDto>.Failure("Account not found");
                }

                // Prevent updating account code or account type (would require revalidation)
                if (!string.IsNullOrWhiteSpace(dto.Name))
                {
                    account.Name = dto.Name;
                }

                if (dto.Description != null)
                {
                    account.Description = dto.Description;
                }

                if (dto.IsActive.HasValue)
                {
                    // Validate: Cannot deactivate account with posted entries
                    if (!dto.IsActive.Value)
                    {
                        var hasEntries = await _context.JournalEntryLines
                            .AnyAsync(jel => jel.AccountId == accountId && jel.JournalEntry.IsPosted);

                        if (hasEntries)
                        {
                            return ServiceResult<AccountDto>.Failure("Cannot deactivate account with posted journal entries");
                        }
                    }

                    account.IsActive = dto.IsActive.Value;
                }

                account.UpdatedAt = DateTime.UtcNow;
                account.UpdatedBy = CurrentUserId;

                await _context.SaveChangesAsync();

                return ServiceResult<AccountDto>.SuccessResult(MapToDto(account));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account");
                return ServiceResult<AccountDto>.Failure($"Error updating account: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> DeleteAccountAsync(Guid accountId)
        {
            try
            {
                var account = await _context.Accounts
                    .Include(a => a.ChildAccounts)
                    .Include(a => a.JournalEntryLines)
                    .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);

                if (account == null)
                {
                    return ServiceResult<bool>.Failure("Account not found");
                }

                // Cannot delete system accounts
                if (account.IsSystem)
                {
                    return ServiceResult<bool>.Failure("Cannot delete system accounts");
                }

                // Cannot delete account with children
                if (account.ChildAccounts.Any())
                {
                    return ServiceResult<bool>.Failure("Cannot delete account with child accounts");
                }

                // Cannot delete account with journal entries
                if (account.JournalEntryLines.Any())
                {
                    return ServiceResult<bool>.Failure("Cannot delete account with journal entries");
                }

                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting account");
                return ServiceResult<bool>.Failure($"Error deleting account: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<AccountDto>>> GetAccountHierarchyAsync()
        {
            try
            {
                var allAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.IsActive)
                    .OrderBy(a => a.AccountCode)
                    .ToListAsync();

                // Fetch all balances for hierarchy
                var balances = await _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Where(jel => jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted)
                    .GroupBy(jel => jel.AccountId)
                    .Select(g => new
                    {
                        AccountId = g.Key,
                        Debit = g.Sum(jel => jel.Debit),
                        Credit = g.Sum(jel => jel.Credit)
                    })
                    .ToListAsync();

                var accountBalances = new Dictionary<Guid, decimal>();
                foreach (var account in allAccounts)
                {
                    var bal = balances.FirstOrDefault(b => b.AccountId == account.Id);
                    var balance = bal != null ? CalculateBalance(account.AccountType, bal.Debit, bal.Credit) : 0m;
                    accountBalances[account.Id] = balance;
                }

                var rootAccounts = allAccounts
                    .Where(a => a.ParentAccountId == null)
                    .Select(a => MapToDtoWithChildren(a, allAccounts, accountBalances))
                    .OrderBy(a => a.AccountCode)
                    .ToList();

                return ServiceResult<List<AccountDto>>.SuccessResult(rootAccounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account hierarchy");
                return ServiceResult<List<AccountDto>>.Failure($"Error getting account hierarchy: {ex.Message}");
            }
        }

        public async Task<ServiceResult<AccountDto>> GetOrCreateAccountForEntityAsync(string entityName, AccountType accountType, Guid? parentAccountId, string? accountCode = null)
        {
            try
            {
                // If account code is not provided, generate one based on parent or sequence
                if (string.IsNullOrWhiteSpace(accountCode))
                {
                    accountCode = await GenerateAccountCodeAsync(parentAccountId, accountType);
                }

                // Check if account already exists with this name and parent
                var existingAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Name == entityName && a.ParentAccountId == parentAccountId);

                if (existingAccount != null)
                {
                    return ServiceResult<AccountDto>.SuccessResult(MapToDto(existingAccount));
                }

                // Create new account
                var createDto = new AccountCreateDto
                {
                    AccountCode = accountCode,
                    Name = entityName,
                    AccountType = accountType,
                    ParentAccountId = parentAccountId,
                    IsActive = true,
                    IsPostable = true // Automatic accounts are leaf accounts initially
                };

                return await CreateAccountAsync(createDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateAccountForEntityAsync");
                return ServiceResult<AccountDto>.Failure($"Error creating automatic account: {ex.Message}");
            }
        }

        private async Task<string> GenerateAccountCodeAsync(Guid? parentAccountId, AccountType accountType)
        {
            string baseCode = "";
            if (parentAccountId.HasValue)
            {
                var parent = await _context.Accounts.FindAsync(parentAccountId.Value);
                if (parent != null)
                {
                    baseCode = parent.AccountCode;
                }
            }
            else
            {
                // Default base codes for account types if no parent
                baseCode = ((int)accountType * 1000).ToString();
            }

            // Find the highest existing code under this parent or with this prefix
            var prefix = baseCode;
            var maxExistingCode = await _context.Accounts
                .Where(a => a.TenantId == TenantId && a.AccountCode.StartsWith(prefix) && a.AccountCode != prefix)
                .OrderByDescending(a => a.AccountCode)
                .Select(a => a.AccountCode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(maxExistingCode))
            {
                return prefix + "01";
            }

            // Try to increment the last part
            if (long.TryParse(maxExistingCode, out long lastCode))
            {
                return (lastCode + 1).ToString();
            }

            return prefix + "_" + Guid.NewGuid().ToString().Substring(0, 4);
        }

        #endregion

        #region Journal Entry Management

        public async Task<ServiceResult<JournalEntryDto>> CreateManualJournalEntryAsync(JournalEntryCreateDto dto)
        {
            var existingTransaction = _context.Database.CurrentTransaction;
            var transaction = existingTransaction == null ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                // Validate lines
                if (dto.Lines == null || dto.Lines.Count < 2)
                {
                    return ServiceResult<JournalEntryDto>.Failure("Journal entry must have at least 2 lines");
                }

                // Validate each line
                foreach (var line in dto.Lines)
                {
                    if (line.Debit < 0 || line.Credit < 0)
                    {
                        return ServiceResult<JournalEntryDto>.Failure("Debit and Credit amounts must be >= 0");
                    }

                    if (line.Debit > 0 && line.Credit > 0)
                    {
                        return ServiceResult<JournalEntryDto>.Failure("Each line must have either Debit or Credit, not both");
                    }

                    if (line.Debit == 0 && line.Credit == 0)
                    {
                        return ServiceResult<JournalEntryDto>.Failure("Each line must have either Debit or Credit amount");
                    }

                    // Validate account exists and is postable
                    var account = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == line.AccountId && a.TenantId == TenantId);

                    if (account == null)
                    {
                        return ServiceResult<JournalEntryDto>.Failure($"Account {line.AccountId} not found");
                    }

                    if (!account.IsActive)
                    {
                        return ServiceResult<JournalEntryDto>.Failure($"Account {account.AccountCode} is not active");
                    }

                    if (!account.CanPost())
                    {
                        return ServiceResult<JournalEntryDto>.Failure($"Account {account.AccountCode} is not postable (must be a leaf account)");
                    }
                }

                // Validate balance
                var totalDebit = dto.Lines.Sum(l => l.Debit);
                var totalCredit = dto.Lines.Sum(l => l.Credit);

                if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                {
                    return ServiceResult<JournalEntryDto>.Failure($"Journal entry is not balanced. Debit: {totalDebit}, Credit: {totalCredit}");
                }

                if (dto.File != null && dto.File.Length > 0)
                {
                    dto.AttachmentUrl = await SaveFile(dto.File, "journal_entries");
                }

                // Generate entry number
                var entryNumber = await GenerateEntryNumberAsync();

                var journalEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    EntryNumber = entryNumber,
                    Date = dto.Date.Date,
                    ReferenceType = JournalEntryReferenceType.Manual,
                    Description = dto.Description,
                    IsPosted = false,
                    AttachmentUrl = dto.AttachmentUrl,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId
                };

                _context.JournalEntries.Add(journalEntry);

                // Create lines
                foreach (var lineDto in dto.Lines)
                {
                    var line = new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = journalEntry.Id,
                        AccountId = lineDto.AccountId,
                        Debit = lineDto.Debit,
                        Credit = lineDto.Credit,
                        Description = lineDto.Description,
                        Reference = lineDto.Reference
                    };

                    _context.JournalEntryLines.Add(line);
                }

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                // Load and return
                var result = await GetJournalEntryAsync(journalEntry.Id);
                return result;
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating manual journal entry");
                return ServiceResult<JournalEntryDto>.Failure($"Error creating journal entry: {ex.Message}");
            }
        }

        public async Task<ServiceResult<JournalEntryDto>> GetJournalEntryAsync(Guid journalEntryId)
        {
            try
            {
                var journalEntry = await _context.JournalEntries
                    .Include(je => je.Lines)
                        .ThenInclude(jel => jel.Account)
                    .Include(je => je.Project)
                    .FirstOrDefaultAsync(je => je.Id == journalEntryId && je.TenantId == TenantId);

                if (journalEntry == null)
                {
                    return ServiceResult<JournalEntryDto>.Failure("Journal entry not found");
                }

                return ServiceResult<JournalEntryDto>.SuccessResult(MapToDto(journalEntry));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal entry");
                return ServiceResult<JournalEntryDto>.Failure($"Error getting journal entry: {ex.Message}");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<JournalEntryDto>>> GetJournalEntriesAsync(JournalEntryFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _context.JournalEntries
                    .Include(je => je.Lines)
                        .ThenInclude(jel => jel.Account)
                    .Include(je => je.Project)
                    .Where(je => je.TenantId == TenantId)
                    .AsQueryable();

                if (filter.FromDate.HasValue)
                {
                    query = query.Where(je => je.Date >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    query = query.Where(je => je.Date <= filter.ToDate.Value);
                }

                if (filter.ReferenceType.HasValue)
                {
                    query = query.Where(je => je.ReferenceType == filter.ReferenceType.Value);
                }

                if (filter.ReferenceId.HasValue)
                {
                    query = query.Where(je => je.ReferenceId == filter.ReferenceId.Value);
                }

                if (filter.IsPosted.HasValue)
                {
                    query = query.Where(je => je.IsPosted == filter.IsPosted.Value);
                }

                if (filter.AccountId.HasValue)
                {
                    query = query.Where(je => je.Lines.Any(l => l.AccountId == filter.AccountId.Value));
                }

                if (!string.IsNullOrWhiteSpace(filter.EntryNumber))
                {
                    query = query.Where(je => je.EntryNumber.Contains(filter.EntryNumber));
                }

                if (filter.ProjectId.HasValue)
                {
                    query = query.Where(je => je.ProjectId == filter.ProjectId.Value);
                }

                var totalCount = await query.CountAsync();

                var journalEntries = await query
                    .OrderByDescending(je => je.Date)
                    .ThenByDescending(je => je.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var journalEntryDtos = journalEntries.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<JournalEntryDto>>.SuccessResult(new PagedResponseDto<JournalEntryDto>
                {
                    Data = journalEntryDtos,
                    TotalCount = totalCount,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal entries");
                return ServiceResult<PagedResponseDto<JournalEntryDto>>.Failure($"Error getting journal entries: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> PostJournalEntryAsync(Guid journalEntryId)
        {
            try
            {
                var journalEntry = await _context.JournalEntries
                    .Include(je => je.Lines)
                    .FirstOrDefaultAsync(je => je.Id == journalEntryId && je.TenantId == TenantId);

                if (journalEntry == null)
                {
                    return ServiceResult<bool>.Failure("Journal entry not found");
                }

                if (journalEntry.IsPosted)
                {
                    return ServiceResult<bool>.Failure("Journal entry is already posted");
                }

                if (!journalEntry.IsBalanced())
                {
                    return ServiceResult<bool>.Failure("Journal entry is not balanced");
                }

                journalEntry.IsPosted = true;
                journalEntry.PostedAt = DateTime.UtcNow;
                journalEntry.PostedBy = CurrentUserId;

                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting journal entry");
                return ServiceResult<bool>.Failure($"Error posting journal entry: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> ReverseJournalEntryAsync(Guid journalEntryId)
        {
            var existingTransaction = _context.Database.CurrentTransaction;
            var transaction = existingTransaction == null ? await _context.Database.BeginTransactionAsync() : null;
            try
            {
                var originalEntry = await _context.JournalEntries
                    .Include(je => je.Lines)
                        .ThenInclude(jel => jel.Account)
                    .FirstOrDefaultAsync(je => je.Id == journalEntryId && je.TenantId == TenantId);

                if (originalEntry == null)
                {
                    return ServiceResult<bool>.Failure("Journal entry not found");
                }

                if (!originalEntry.IsPosted)
                {
                    return ServiceResult<bool>.Failure("Can only reverse posted journal entries");
                }

                // Check if already reversed
                if (originalEntry.ReversingEntryId.HasValue)
                {
                    return ServiceResult<bool>.Failure("Journal entry has already been reversed");
                }

                // Generate entry number for reversing entry
                var entryNumber = await GenerateEntryNumberAsync();

                var reversingEntry = new JournalEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    EntryNumber = entryNumber,
                    Date = DateTime.UtcNow.Date,
                    ReferenceType = JournalEntryReferenceType.Reversing,
                    ReferenceId = originalEntry.Id,
                    Description = $"Reversal of {originalEntry.EntryNumber}: {originalEntry.Description}",
                    IsPosted = true, // Auto-post reversing entries
                    PostedAt = DateTime.UtcNow,
                    PostedBy = CurrentUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = CurrentUserId,
                    ReversingEntryId = originalEntry.Id
                };

                _context.JournalEntries.Add(reversingEntry);

                // Create reversed lines (swap debit and credit)
                foreach (var originalLine in originalEntry.Lines)
                {
                    var reversedLine = new JournalEntryLine
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = reversingEntry.Id,
                        AccountId = originalLine.AccountId,
                        Debit = originalLine.Credit, // Swap
                        Credit = originalLine.Debit,   // Swap
                        Description = $"Reversal: {originalLine.Description}",
                        Reference = originalLine.Reference
                    };

                    _context.JournalEntryLines.Add(reversedLine);
                }

                // Link original entry to reversing entry
                originalEntry.ReversingEntryId = reversingEntry.Id;

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                _logger.LogError(ex, "Error reversing journal entry");
                return ServiceResult<bool>.Failure($"Error reversing journal entry: {ex.Message}");
            }
        }

        #endregion

        #region General Ledger Queries

        public async Task<ServiceResult<AccountBalanceDto>> GetAccountBalanceAsync(Guid accountId, DateTime? asOfDate = null)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);

                if (account == null)
                {
                    return ServiceResult<AccountBalanceDto>.Failure("Account not found");
                }

                var query = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Where(jel => jel.AccountId == accountId && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted);

                if (asOfDate.HasValue)
                {
                    query = query.Where(jel => jel.JournalEntry.Date <= asOfDate.Value);
                }

                var debitTotal = await query.SumAsync(jel => jel.Debit);
                var creditTotal = await query.SumAsync(jel => jel.Credit);

                var balance = CalculateBalance(account.AccountType, debitTotal, creditTotal);

                return ServiceResult<AccountBalanceDto>.SuccessResult(new AccountBalanceDto
                {
                    AccountId = account.Id,
                    AccountCode = account.AccountCode,
                    AccountName = account.Name,
                    DebitTotal = debitTotal,
                    CreditTotal = creditTotal,
                    Balance = balance,
                    AsOfDate = asOfDate ?? DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account balance");
                return ServiceResult<AccountBalanceDto>.Failure($"Error getting account balance: {ex.Message}");
            }
        }

        public async Task<ServiceResult<TrialBalanceDto>> GetTrialBalanceAsync(DateTime? asOfDate = null)
        {
            try
            {
                var query = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Include(jel => jel.Account)
                    .Where(jel => jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted);

                if (asOfDate.HasValue)
                {
                    query = query.Where(jel => jel.JournalEntry.Date <= asOfDate.Value);
                }

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
                    DebitTotal = ab.DebitTotal,
                    CreditTotal = ab.CreditTotal,
                    Balance = CalculateBalance(ab.AccountType, ab.DebitTotal, ab.CreditTotal)
                }).OrderBy(i => i.AccountCode).ToList();

                var totalDebit = items.Sum(i => i.DebitTotal);
                var totalCredit = items.Sum(i => i.CreditTotal);

                return ServiceResult<TrialBalanceDto>.SuccessResult(new TrialBalanceDto
                {
                    Items = items,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    IsBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m,
                    AsOfDate = asOfDate ?? DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trial balance");
                return ServiceResult<TrialBalanceDto>.Failure($"Error getting trial balance: {ex.Message}");
            }
        }

        public async Task<ServiceResult<LedgerDto>> GetAccountLedgerAsync(Guid accountId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId);

                if (account == null)
                {
                    return ServiceResult<LedgerDto>.Failure("Account not found");
                }

                // Calculate opening balance (before fromDate)
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

                // Get entries in date range
                var entriesQuery = _context.JournalEntryLines
                    .Include(jel => jel.JournalEntry)
                    .Where(jel => jel.AccountId == accountId && jel.JournalEntry.TenantId == TenantId && jel.JournalEntry.IsPosted)
                    .AsQueryable();

                if (fromDate.HasValue)
                {
                    entriesQuery = entriesQuery.Where(jel => jel.JournalEntry.Date >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    entriesQuery = entriesQuery.Where(jel => jel.JournalEntry.Date <= toDate.Value);
                }

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
                    AccountId = account.Id,
                    AccountCode = account.AccountCode,
                    AccountName = account.Name,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Entries = ledgerEntries,
                    OpeningBalance = openingBalance,
                    ClosingBalance = runningBalance
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account ledger");
                return ServiceResult<LedgerDto>.Failure($"Error getting account ledger: {ex.Message}");
            }
        }

        public async Task<ServiceResult<ProfitAndLossDto>> GetProfitAndLossAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var revenueAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Revenue && a.IsActive)
                    .ToListAsync();

                var expenseAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Expense && a.IsActive)
                    .ToListAsync();

                var revenueItems = new List<ProfitAndLossItemDto>();
                var expenseItems = new List<ProfitAndLossItemDto>();

                // Calculate revenue
                foreach (var account in revenueAccounts)
                {
                    var balanceResult = await GetAccountBalanceAsync(account.Id, toDate);
                    if (balanceResult.Success && balanceResult.Data.Balance != 0)
                    {
                        revenueItems.Add(new ProfitAndLossItemDto
                        {
                            AccountId = account.Id,
                            AccountCode = account.AccountCode,
                            AccountName = account.Name,
                            Amount = balanceResult.Data.Balance
                        });
                    }
                }

                // Calculate expenses
                foreach (var account in expenseAccounts)
                {
                    var balanceResult = await GetAccountBalanceAsync(account.Id, toDate);
                    if (balanceResult.Success && balanceResult.Data.Balance != 0)
                    {
                        expenseItems.Add(new ProfitAndLossItemDto
                        {
                            AccountId = account.Id,
                            AccountCode = account.AccountCode,
                            AccountName = account.Name,
                            Amount = balanceResult.Data.Balance
                        });
                    }
                }

                var totalRevenue = revenueItems.Sum(i => i.Amount);
                var totalExpenses = expenseItems.Sum(i => i.Amount);
                var netIncome = totalRevenue - totalExpenses;

                return ServiceResult<ProfitAndLossDto>.SuccessResult(new ProfitAndLossDto
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    RevenueItems = revenueItems.OrderBy(i => i.AccountCode).ToList(),
                    ExpenseItems = expenseItems.OrderBy(i => i.AccountCode).ToList(),
                    TotalRevenue = totalRevenue,
                    TotalExpenses = totalExpenses,
                    NetIncome = netIncome
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profit and loss");
                return ServiceResult<ProfitAndLossDto>.Failure($"Error getting profit and loss: {ex.Message}");
            }
        }

        public async Task<ServiceResult<BalanceSheetDto>> GetBalanceSheetAsync(DateTime asOfDate)
        {
            try
            {
                var assetAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Asset && a.IsActive)
                    .ToListAsync();

                var liabilityAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Liability && a.IsActive)
                    .ToListAsync();

                var equityAccounts = await _context.Accounts
                    .Where(a => a.TenantId == TenantId && a.AccountType == AccountType.Equity && a.IsActive)
                    .ToListAsync();

                var assets = new List<BalanceSheetItemDto>();
                var liabilities = new List<BalanceSheetItemDto>();
                var equity = new List<BalanceSheetItemDto>();

                // Calculate assets
                foreach (var account in assetAccounts)
                {
                    var balanceResult = await GetAccountBalanceAsync(account.Id, asOfDate);
                    if (balanceResult.Success && balanceResult.Data.Balance != 0)
                    {
                        assets.Add(new BalanceSheetItemDto
                        {
                            AccountId = account.Id,
                            AccountCode = account.AccountCode,
                            AccountName = account.Name,
                            Balance = balanceResult.Data.Balance
                        });
                    }
                }

                // Calculate liabilities
                foreach (var account in liabilityAccounts)
                {
                    var balanceResult = await GetAccountBalanceAsync(account.Id, asOfDate);
                    if (balanceResult.Success && balanceResult.Data.Balance != 0)
                    {
                        liabilities.Add(new BalanceSheetItemDto
                        {
                            AccountId = account.Id,
                            AccountCode = account.AccountCode,
                            AccountName = account.Name,
                            Balance = balanceResult.Data.Balance
                        });
                    }
                }

                // Calculate equity
                foreach (var account in equityAccounts)
                {
                    var balanceResult = await GetAccountBalanceAsync(account.Id, asOfDate);
                    if (balanceResult.Success && balanceResult.Data.Balance != 0)
                    {
                        equity.Add(new BalanceSheetItemDto
                        {
                            AccountId = account.Id,
                            AccountCode = account.AccountCode,
                            AccountName = account.Name,
                            Balance = balanceResult.Data.Balance
                        });
                    }
                }

                var totalAssets = assets.Sum(a => a.Balance);
                var totalLiabilities = liabilities.Sum(l => l.Balance);
                var totalEquity = equity.Sum(e => e.Balance);
                var isBalanced = Math.Abs(totalAssets - (totalLiabilities + totalEquity)) < 0.01m;

                return ServiceResult<BalanceSheetDto>.SuccessResult(new BalanceSheetDto
                {
                    AsOfDate = asOfDate,
                    Assets = assets.OrderBy(a => a.AccountCode).ToList(),
                    Liabilities = liabilities.OrderBy(l => l.AccountCode).ToList(),
                    Equity = equity.OrderBy(e => e.AccountCode).ToList(),
                    TotalAssets = totalAssets,
                    TotalLiabilities = totalLiabilities,
                    TotalEquity = totalEquity,
                    IsBalanced = isBalanced
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balance sheet");
                return ServiceResult<BalanceSheetDto>.Failure($"Error getting balance sheet: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private async Task<string> GenerateEntryNumberAsync()
        {
            var lastEntry = await _context.JournalEntries
                .Where(je => je.TenantId == TenantId)
                .OrderByDescending(je => je.CreatedAt)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastEntry != null)
            {
                // Extract number from entry number (e.g., "JE-0001" -> 1)
                var parts = lastEntry.EntryNumber.Split('-');
                if (parts.Length > 1 && int.TryParse(parts[1], out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"JE-{nextNumber:D4}";
        }

        private AccountDto MapToDto(Account account, decimal balance = 0)
        {
            return new AccountDto
            {
                Id = account.Id,
                TenantId = account.TenantId,
                AccountCode = account.AccountCode,
                Name = account.Name,
                AccountType = account.AccountType,
                AccountTypeName = account.AccountType.ToString(),
                ParentAccountId = account.ParentAccountId,
                ParentAccountName = account.ParentAccount?.Name,
                Level = account.Level,
                IsActive = account.IsActive,
                IsPostable = account.IsPostable,
                IsSystem = account.IsSystem,
                Description = account.Description,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                Balance = balance
            };
        }

        private AccountDto MapToDtoWithChildren(Account account, List<Account> allAccounts, Dictionary<Guid, decimal> accountBalances)
        {
            var balance = accountBalances.ContainsKey(account.Id) ? accountBalances[account.Id] : 0;
            var dto = MapToDto(account, balance);
            dto.ChildAccounts = allAccounts
                .Where(a => a.ParentAccountId == account.Id)
                .Select(a => MapToDtoWithChildren(a, allAccounts, accountBalances))
                .ToList();

            // Roll up child balances to parent if parent is not postable
            if (!account.IsPostable && dto.ChildAccounts.Any())
            {
                dto.Balance = dto.ChildAccounts.Sum(c => c.Balance);
            }

            return dto;
        }

        private JournalEntryDto MapToDto(JournalEntry journalEntry)
        {
            return new JournalEntryDto
            {
                Id = journalEntry.Id,
                TenantId = journalEntry.TenantId,
                EntryNumber = journalEntry.EntryNumber,
                Date = journalEntry.Date,
                ReferenceType = journalEntry.ReferenceType,
                ReferenceTypeName = journalEntry.ReferenceType.ToString(),
                ReferenceId = journalEntry.ReferenceId,
                Description = journalEntry.Description,
                IsPosted = journalEntry.IsPosted,
                PostedAt = journalEntry.PostedAt,
                CreatedBy = journalEntry.CreatedBy,
                PostedBy = journalEntry.PostedBy,
                CreatedAt = journalEntry.CreatedAt,
                UpdatedAt = journalEntry.UpdatedAt,
                ReversingEntryId = journalEntry.ReversingEntryId,
                ProjectId = journalEntry.ProjectId,
                ProjectName = journalEntry.Project?.Name,
                Lines = journalEntry.Lines.Select(MapToDto).ToList(),
                TotalDebit = journalEntry.TotalDebit,
                TotalCredit = journalEntry.TotalCredit,
                AttachmentUrl = journalEntry.AttachmentUrl
            };
        }

        private JournalEntryLineDto MapToDto(JournalEntryLine line)
        {
            return new JournalEntryLineDto
            {
                Id = line.Id,
                JournalEntryId = line.JournalEntryId,
                AccountId = line.AccountId,
                AccountCode = line.Account?.AccountCode ?? string.Empty,
                AccountName = line.Account?.Name ?? string.Empty,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description,
                Reference = line.Reference
            };
        }

        /// <summary>
        /// Calculates account balance based on account type.
        /// Assets & Expenses: Balance = Debit - Credit
        /// Liabilities, Equity, Revenue: Balance = Credit - Debit
        /// </summary>
        private decimal CalculateBalance(AccountType accountType, decimal debit, decimal credit)
        {
            return accountType switch
            {
                AccountType.Asset or AccountType.Expense => debit - credit,
                AccountType.Liability or AccountType.Equity or AccountType.Revenue => credit - debit,
                _ => 0
            };
        }

        /// <summary>
        /// Calculates balance change for running balance calculation.
        /// </summary>
        private decimal CalculateBalanceChange(AccountType accountType, decimal debit, decimal credit)
        {
            return accountType switch
            {
                AccountType.Asset or AccountType.Expense => debit - credit,
                AccountType.Liability or AccountType.Equity or AccountType.Revenue => credit - debit,
                _ => 0
            };
        }

        #endregion
        private async Task<string> SaveFile(IFormFile file, string subFolder)
        {
            var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", subFolder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return Path.Combine("uploads", subFolder, uniqueFileName);
        }

        private void DeleteFile(string filePath)
        {
            var fullPath = Path.Combine(_hostingEnvironment.WebRootPath, filePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}

