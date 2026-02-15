using fatortak.Dtos;
using fatortak.Dtos.Shared;

namespace fatortak.Services.BranchService
{
    public interface IBranchService
    {
        Task<ServiceResult<BranchDto>> CreateBranchAsync(CreateBranchDto dto);
        Task<ServiceResult<List<BranchDto>>> GetBranchesAsync();
        Task<ServiceResult<BranchDto>> GetBranchAsync(Guid id);
        Task<ServiceResult<BranchDto>> UpdateBranchAsync(Guid id, UpdateBranchDto dto);
        Task<ServiceResult<bool>> DeleteBranchAsync(Guid id);
        Task<ServiceResult<bool>> ToggleActivationAsync(Guid id);
        Task<ServiceResult<BranchDto>> GetMainBranchAsync();
    }
}
