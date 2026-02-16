using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Dtos.Transaction;

namespace fatortak.Services.TransactionService
{
    public interface ITransactionService
    {
        Task<ServiceResult<Transaction>> AddTransactionAsync(Transaction transaction);
        Task<ServiceResult<PagedResponseDto<Transaction>>> GetTransactionsAsync(TransactionFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<decimal>> GetBalanceAsync();
        Task<ServiceResult<bool>> DeleteTransactionByReferenceAsync(string referenceId, string referenceType);
        Task<ServiceResult<Transaction>> UpdateTransactionByReferenceAsync(string referenceId, string referenceType, Transaction transaction);
        Task<ServiceResult<bool>> TransferAsync(TransferDto transferDto);
    }
}
