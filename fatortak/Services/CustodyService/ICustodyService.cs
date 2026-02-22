using fatortak.Dtos.Shared;

namespace fatortak.Services.CustodyService
{
    /// <summary>
    /// Service for handling employee custody (advances) using Chart of Accounts.
    /// Custody is tracked as an Asset account in the accounting system.
    /// </summary>
    public interface ICustodyService
    {
        /// <summary>
        /// Give custody using an account ID directly.
        /// </summary>
        Task<ServiceResult<bool>> GiveCustodyByAccountAsync(Guid accountId, decimal amount, Guid? sourceAccountId, string? description);

        /// <summary>
        /// Return custody using an account ID directly.
        /// </summary>
        Task<ServiceResult<bool>> ReturnCustodyByAccountAsync(Guid accountId, decimal amount, Guid? destinationAccountId, string? description);

        /// <summary>
        /// Replenish custody using an account ID directly.
        /// </summary>
        Task<ServiceResult<bool>> ReplenishCustodyByAccountAsync(Guid accountId, decimal amount, Guid? sourceAccountId, string? description);

        /// <summary>
        /// Create a new custody account under the "Employee Custody" parent.
        /// </summary>
        Task<fatortak.Dtos.Accounting.AccountDto> CreateCustodyAccountAsync(string name, string? description);
    }
}

