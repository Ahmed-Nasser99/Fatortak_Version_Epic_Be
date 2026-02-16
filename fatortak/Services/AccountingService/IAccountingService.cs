using fatortak.Dtos.Accounting;
using fatortak.Dtos.Shared;

namespace fatortak.Services.AccountingService
{
    /// <summary>
    /// Service interface for accounting operations and general ledger queries
    /// </summary>
    public interface IAccountingService
    {
        // Account management
        Task<ServiceResult<AccountDto>> CreateAccountAsync(AccountCreateDto dto);
        Task<ServiceResult<AccountDto>> GetAccountAsync(Guid accountId);
        Task<ServiceResult<PagedResponseDto<AccountDto>>> GetAccountsAsync(AccountFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<AccountDto>> UpdateAccountAsync(Guid accountId, AccountUpdateDto dto);
        Task<ServiceResult<bool>> DeleteAccountAsync(Guid accountId);
        Task<ServiceResult<List<AccountDto>>> GetAccountHierarchyAsync();

        // Journal Entry management
        Task<ServiceResult<JournalEntryDto>> CreateManualJournalEntryAsync(JournalEntryCreateDto dto);
        Task<ServiceResult<JournalEntryDto>> GetJournalEntryAsync(Guid journalEntryId);
        Task<ServiceResult<PagedResponseDto<JournalEntryDto>>> GetJournalEntriesAsync(JournalEntryFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<bool>> PostJournalEntryAsync(Guid journalEntryId);
        Task<ServiceResult<bool>> ReverseJournalEntryAsync(Guid journalEntryId);

        // General Ledger Queries
        Task<ServiceResult<AccountBalanceDto>> GetAccountBalanceAsync(Guid accountId, DateTime? asOfDate = null);
        Task<ServiceResult<TrialBalanceDto>> GetTrialBalanceAsync(DateTime? asOfDate = null);
        Task<ServiceResult<LedgerDto>> GetAccountLedgerAsync(Guid accountId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<ServiceResult<ProfitAndLossDto>> GetProfitAndLossAsync(DateTime fromDate, DateTime toDate);
        Task<ServiceResult<BalanceSheetDto>> GetBalanceSheetAsync(DateTime asOfDate);
    }
}

