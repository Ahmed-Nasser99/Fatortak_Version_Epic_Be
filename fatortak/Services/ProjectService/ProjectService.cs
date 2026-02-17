using fatortak.Context;
using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using fatortak.Common.Enum;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.ProjectService
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ProjectService> _logger;
        private readonly Services.AccountingService.IAccountingService _accountingService;

        public ProjectService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<ProjectService> logger, Services.AccountingService.IAccountingService accountingService)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _accountingService = accountingService;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<ProjectDto>> CreateProjectAsync(CreateProjectDto dto)
        {
            try
            {
                var project = new Project
                {
                    TenantId = TenantId,
                    Name = dto.Name,
                    Description = dto.Description,
                    CustomerId = dto.CustomerId,
                    Status = dto.Status,
                    TotalBudget = dto.Budget,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Projects.AddAsync(project);
                await _context.SaveChangesAsync();

                // Create automatic account
                if (dto.CustomerId.HasValue)
                {
                    try
                    {
                        var customer = await _context.Customers.FindAsync(dto.CustomerId.Value);
                        if (customer != null && customer.AccountId.HasValue)
                        {
                            var accountType = customer.IsSupplier ? Common.Enum.AccountType.Liability : Common.Enum.AccountType.Asset;
                            var accountResult = await _accountingService.GetOrCreateAccountForEntityAsync(project.Name, accountType, customer.AccountId.Value);
                            if (accountResult.Success)
                            {
                                project.AccountId = accountResult.Data.Id;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create automatic account for project {ProjectId}", project.Id);
                    }
                }

                // Explicitly load Customer if needed, or just map what we have
                if (dto.CustomerId.HasValue)
                {
                    project.Customer = await _context.Customers.FindAsync(dto.CustomerId);
                }

                return ServiceResult<ProjectDto>.SuccessResult(MapToDto(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                return ServiceResult<ProjectDto>.Failure("Failed to create project");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<ProjectDto>>> GetProjectsAsync(PaginationDto pagination, string? name = null, Guid? customerId = null)
        {
            try
            {
                var query = _context.Projects
                    .Include(p => p.Customer)
                    .Where(p => p.TenantId == TenantId)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(p => p.Name.Contains(name));

                if (customerId.HasValue)
                    query = query.Where(p => p.CustomerId == customerId);

                var totalCount = await query.CountAsync();

                var projects = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var dtos = projects.Select(MapToDto).ToList();

                return ServiceResult<PagedResponseDto<ProjectDto>>.SuccessResult(new PagedResponseDto<ProjectDto>
                {
                    Data = dtos,
                    TotalCount = totalCount,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting projects");
                return ServiceResult<PagedResponseDto<ProjectDto>>.Failure("Failed to get projects");
            }
        }

        public async Task<ServiceResult<ProjectDto>> GetProjectAsync(Guid projectId)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<ProjectDto>.Failure("Project not found");

                return ServiceResult<ProjectDto>.SuccessResult(MapToDto(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project");
                return ServiceResult<ProjectDto>.Failure("Failed to get project");
            }
        }

        public async Task<ServiceResult<ProjectDto>> UpdateProjectStatusAsync(Guid projectId, ProjectStatus status)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<ProjectDto>.Failure("Project not found");

                project.Status = status;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<ProjectDto>.SuccessResult(MapToDto(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project status");
                return ServiceResult<ProjectDto>.Failure("Failed to update project status");
            }
        }

        public async Task<ServiceResult<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto)
        {
            try
            {
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<ProjectDto>.Failure("Project not found");

                project.Name = dto.Name;
                project.Description = dto.Description;
                project.CustomerId = dto.CustomerId;
                project.Status = dto.Status;
                project.TotalBudget = dto.Budget;
                project.StartDate = dto.StartDate;
                project.EndDate = dto.EndDate;
                project.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                 if (dto.CustomerId.HasValue)
                {
                    project.Customer = await _context.Customers.FindAsync(dto.CustomerId);
                }

                return ServiceResult<ProjectDto>.SuccessResult(MapToDto(project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project");
                return ServiceResult<ProjectDto>.Failure("Failed to update project");
            }
        }

        public async Task<ServiceResult<bool>> DeleteProjectAsync(Guid projectId)
        {
            try
            {
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.TenantId == TenantId);

                if (project == null)
                    return ServiceResult<bool>.Failure("Project not found");

                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project");
                return ServiceResult<bool>.Failure("Failed to delete project");
            }
        }

        private ProjectDto MapToDto(Project project)
        {
            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CustomerId = project.CustomerId,
                CustomerName = project.Customer?.Name,
                Status = project.Status,
                TotalBudget = project.TotalBudget,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                IsInternal = project.IsInternal,
                CreatedAt = project.CreatedAt
            };
        }
    }
}
