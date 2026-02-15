using fatortak.Dtos.HR.Departments;
using fatortak.Dtos.Shared;

namespace fatortak.Services.HR.DepartmentService
{
    public interface IDepartmentService
    {
        Task<ServiceResult<PagedResponseDto<DepartmentDto>>> GetAllDepartmentsAsync(PaginationDto pagination, DepartmentFilterDto filter);
        Task<ServiceResult<DepartmentDto>> GetDepartmentByIdAsync(Guid id);
        Task<ServiceResult<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentDto dto);
        Task<ServiceResult<DepartmentDto>> UpdateDepartmentAsync(Guid id, UpdateDepartmentDto dto);
        Task<ServiceResult<bool>> DeleteDepartmentAsync(Guid id);
    }
}
