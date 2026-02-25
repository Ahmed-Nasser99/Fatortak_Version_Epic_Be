using fatortak.Dtos.Cheque;
using fatortak.Dtos.Shared;

namespace fatortak.Services.ChequeService
{
    public interface IChequeService
    {
        Task<ServiceResult<PagedResponseDto<ChequeDto>>> GetChequesAsync(PaginationDto pagination, string? status = null);
        Task<ServiceResult<ChequeDto>> UpdateChequeStatusAsync(Guid chequeId, UpdateChequeStatusDto dto);
    }
}
