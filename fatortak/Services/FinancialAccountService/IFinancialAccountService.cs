using fatortak.Dtos;
using fatortak.Dtos.Shared;

namespace fatortak.Services.FinancialAccountService
{
    public interface IFinancialAccountService
    {
        Task<ServiceResult<FinancialAccountDto>> CreateAccountAsync(CreateFinancialAccountDto dto);
        Task<ServiceResult<PagedResponseDto<FinancialAccountDto>>> GetAccountsAsync(PaginationDto pagination, string? name = null);
        Task<ServiceResult<FinancialAccountDto>> GetAccountAsync(Guid accountId);
        Task<ServiceResult<FinancialAccountDto>> UpdateAccountAsync(Guid accountId, UpdateFinancialAccountDto dto);
        Task<ServiceResult<bool>> DeleteAccountAsync(Guid accountId);
    }
}
