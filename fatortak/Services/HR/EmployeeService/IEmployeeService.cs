using fatortak.Dtos.HR.Employee;
using fatortak.Dtos.Shared;

namespace fatortak.Services.HR.EmployeeService
{
    public interface IEmployeeService
    {
        Task<ServiceResult<PagedResponseDto<EmployeeDto>>> GetAllEmployeesAsync(PaginationDto pagination, EmployeeFilterDto filter);
        Task<ServiceResult<EmployeeDto>> GetEmployeeByIdAsync(Guid id);
        Task<ServiceResult<EmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto dto);
        Task<ServiceResult<EmployeeDto>> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto dto);
        Task<ServiceResult<bool>> DeleteEmployeeAsync(Guid id);
    }

}
