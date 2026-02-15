using fatortak.Dtos;
using fatortak.Dtos.Shared;

namespace fatortak.Services.HR
{
    public interface IPayrollService
    {
        Task<ServiceResult<PayrollDto>> GeneratePayrollAsync(GeneratePayrollDto dto);
        Task<ServiceResult<PayrollDto>> GetPayrollAsync(Guid id);
        Task<ServiceResult<List<PayrollDto>>> GetAllPayrollsAsync();
        Task<ServiceResult<PayrollDto>> SubmitPayrollAsync(Guid id);
        Task<ServiceResult<bool>> DeletePayrollAsync(Guid id);
    }
}
