using fatortak.Context;
using fatortak.Dtos.HR.Departments;
using fatortak.Dtos.Invoice;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.HR.DepartmentService
{
    public class DepartmentService : IDepartmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DepartmentService> _logger;

        public DepartmentService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DepartmentService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<PagedResponseDto<DepartmentDto>>> GetAllDepartmentsAsync(PaginationDto pagination , DepartmentFilterDto filter)
        {
            try
            {
                var query = _context.Departments
                    .Include(d => d.Employees)
                    .Where(d => d.TenantId == _tenantId);


                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var searchTerm = filter.Search.ToLower();
                    query = query.Where(d =>
                        d.Name.ToLower().Contains(searchTerm) ||
                        (d.Description != null && d.Description.ToLower().Contains(searchTerm))
                    );
                }

                var totalCount = await query.CountAsync();

                var departments = await query
                    .OrderBy(d => d.Name)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .Select(d => new DepartmentDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Description = d.Description,
                        EmployeesCount = d.Employees.Count
                    })
                    .ToListAsync();

                return ServiceResult<PagedResponseDto<DepartmentDto>>.SuccessResult(new PagedResponseDto<DepartmentDto>
                {
                    Data = departments,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting departments");
                return ServiceResult<PagedResponseDto<DepartmentDto>>.Failure("Failed to load departments");
            }
        }

        public async Task<ServiceResult<DepartmentDto>> GetDepartmentByIdAsync(Guid id)
        {
            try
            {
                var department = await _context.Departments
                     .Include(d => d.Employees)
                    .Where(d => d.TenantId == _tenantId && d.Id == id)
                    .Select(d => new DepartmentDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Description = d.Description,
                        EmployeesCount = d.Employees.Count
                    })
                    .FirstOrDefaultAsync();

                return department == null
                    ? ServiceResult<DepartmentDto>.Failure("Department not found")
                    : ServiceResult<DepartmentDto>.SuccessResult(department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department by ID");
                return ServiceResult<DepartmentDto>.Failure("Failed to load department");
            }
        }

        public async Task<ServiceResult<DepartmentDto>> CreateDepartmentAsync(CreateDepartmentDto dto)
        {
            try
            {
                var department = new Department
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    TenantId = _tenantId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();

                return ServiceResult<DepartmentDto>.SuccessResult(new DepartmentDto
                {
                    Id = department.Id,
                    Name = department.Name,
                    Description = department.Description
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating department");
                return ServiceResult<DepartmentDto>.Failure("Failed to create department");
            }
        }

        public async Task<ServiceResult<DepartmentDto>> UpdateDepartmentAsync(Guid id, UpdateDepartmentDto dto)
        {
            try
            {
                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.TenantId == _tenantId && d.Id == id);

                if (department == null)
                    return ServiceResult<DepartmentDto>.Failure("Department not found");

                if (!string.IsNullOrWhiteSpace(dto.Name))
                    department.Name = dto.Name;

                if (!string.IsNullOrWhiteSpace(dto.Description))
                    department.Description = dto.Description;

                department.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<DepartmentDto>.SuccessResult(new DepartmentDto
                {
                    Id = department.Id,
                    Name = department.Name,
                    Description = department.Description
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating department");
                return ServiceResult<DepartmentDto>.Failure("Failed to update department");
            }
        }

        public async Task<ServiceResult<bool>> DeleteDepartmentAsync(Guid id)
        {
            try
            {
                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.TenantId == _tenantId && d.Id == id);

                if (department == null)
                    return ServiceResult<bool>.Failure("Department not found");

                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department");
                return ServiceResult<bool>.Failure("Failed to delete department");
            }
        }
    }
}
