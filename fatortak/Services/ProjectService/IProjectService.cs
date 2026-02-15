using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Common.Enum;

namespace fatortak.Services.ProjectService
{
    public interface IProjectService
    {
        Task<ServiceResult<ProjectDto>> CreateProjectAsync(CreateProjectDto dto);
        Task<ServiceResult<PagedResponseDto<ProjectDto>>> GetProjectsAsync(PaginationDto pagination, string? name = null, Guid? customerId = null);
        Task<ServiceResult<ProjectDto>> GetProjectAsync(Guid projectId);
        Task<ServiceResult<ProjectDto>> UpdateProjectStatusAsync(Guid projectId, ProjectStatus status);
        Task<ServiceResult<ProjectDto>> UpdateProjectAsync(Guid projectId, UpdateProjectDto dto);
        Task<ServiceResult<bool>> DeleteProjectAsync(Guid projectId);
    }
}
