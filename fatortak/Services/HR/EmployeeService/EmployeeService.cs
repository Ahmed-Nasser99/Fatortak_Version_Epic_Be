using fatortak.Context;
using fatortak.Dtos.HR.Employee;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.HR.EmployeeService
{
    public class EmployeeService : IEmployeeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<EmployeeService> _logger;

        public EmployeeService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<EmployeeService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<PagedResponseDto<EmployeeDto>>> GetAllEmployeesAsync(PaginationDto pagination, EmployeeFilterDto filter)
        {
            try
            {
                var query = _context.Employees
                    .Include(e => e.Department)
                    .Where(e => e.TenantId == _tenantId);

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    query = query.Where(e =>
                        e.FullName.ToLower().Contains(searchTerm) ||
                        (e.Email != null && e.Email.ToLower().Contains(searchTerm))
                    );
                }
                var totalCount = await query.CountAsync();

                var employees = await query
                    .OrderBy(e => e.FullName)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .Select(e => new EmployeeDto
                    {
                        Id = e.Id,
                        FullName = e.FullName,
                        Email = e.Email,
                        Phone = e.Phone,
                        Position = e.Position,
                        DepartmentName = e.Department != null ? e.Department.Name : string.Empty,
                        DepartmentId = e.DepartmentId,
                        HireDate = e.HireDate,
                        Salary = e.Salary
                    })
                    .ToListAsync();

                return ServiceResult<PagedResponseDto<EmployeeDto>>.SuccessResult(new PagedResponseDto<EmployeeDto>
                {
                    Data = employees,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees");
                return ServiceResult<PagedResponseDto<EmployeeDto>>.Failure("Failed to load employees");
            }
        }

        public async Task<ServiceResult<EmployeeDto>> GetEmployeeByIdAsync(Guid id)
        {
            try
            {
                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .Where(e => e.TenantId == _tenantId && e.Id == id)
                    .Select(e => new EmployeeDto
                    {
                        Id = e.Id,
                        FullName = e.FullName,
                        Email = e.Email ?? string.Empty,
                        Phone = e.Phone ?? string.Empty,
                        Position = e.Position ?? string.Empty,
                        DepartmentName = e.Department != null ? e.Department.Name : string.Empty,
                        DepartmentId = e.DepartmentId,
                        HireDate = e.HireDate,
                        Salary = e.Salary
                    })
                    .FirstOrDefaultAsync();

                return employee == null
                    ? ServiceResult<EmployeeDto>.Failure("Employee not found")
                    : ServiceResult<EmployeeDto>.SuccessResult(employee);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee by ID");
                return ServiceResult<EmployeeDto>.Failure("Failed to load employee");
            }
        }

        public async Task<ServiceResult<EmployeeDto>> CreateEmployeeAsync(CreateEmployeeDto dto)
        {
            try
            {
                var employee = new Employee
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Position = dto.Position,
                    HireDate = dto.HireDate,
                    Salary = dto.Salary,
                    DepartmentId = dto.DepartmentId,
                    TenantId = _tenantId
                };

                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();

                return ServiceResult<EmployeeDto>.SuccessResult(new EmployeeDto
                {
                    Id = employee.Id,
                    FullName = employee.FullName,
                    Email = employee.Email ?? string.Empty,
                    Phone = employee.Phone ?? string.Empty,
                    Position = employee.Position ?? string.Empty,
                    DepartmentName = await _context.Departments
                        .Where(d => d.Id == employee.DepartmentId)
                        .Select(d => d.Name)
                        .FirstOrDefaultAsync() ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee");
                return ServiceResult<EmployeeDto>.Failure("Failed to create employee");
            }
        }

        public async Task<ServiceResult<EmployeeDto>> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto dto)
        {
            try
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.TenantId == _tenantId && e.Id == id);

                if (employee == null)
                    return ServiceResult<EmployeeDto>.Failure("Employee not found");

                if (!string.IsNullOrWhiteSpace(dto.FullName))
                    employee.FullName = dto.FullName;

                if (!string.IsNullOrWhiteSpace(dto.Email))
                    employee.Email = dto.Email;

                if (!string.IsNullOrWhiteSpace(dto.Phone))
                    employee.Phone = dto.Phone;

                if (!string.IsNullOrWhiteSpace(dto.Position))
                    employee.Position = dto.Position;

                if (dto.HireDate.HasValue)
                    employee.HireDate = dto.HireDate.Value;

                if (dto.Salary.HasValue)
                    employee.Salary = dto.Salary.Value;

                if (dto.DepartmentId.HasValue)
                    employee.DepartmentId = dto.DepartmentId.Value;

                employee.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<EmployeeDto>.SuccessResult(new EmployeeDto
                {
                    Id = employee.Id,
                    FullName = employee.FullName,
                    Email = employee.Email ?? string.Empty,
                    Phone = employee.Phone ?? string.Empty,
                    Position = employee.Position ?? string.Empty,
                    DepartmentName = await _context.Departments
                        .Where(d => d.Id == employee.DepartmentId)
                        .Select(d => d.Name)
                        .FirstOrDefaultAsync() ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee");
                return ServiceResult<EmployeeDto>.Failure("Failed to update employee");
            }
        }

        public async Task<ServiceResult<bool>> DeleteEmployeeAsync(Guid id)
        {
            try
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.TenantId == _tenantId && e.Id == id);

                if (employee == null)
                    return ServiceResult<bool>.Failure("Employee not found");

                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee");
                return ServiceResult<bool>.Failure("Failed to delete employee");
            }
        }
    }
}
