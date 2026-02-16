using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Services.ProjectService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> CreateProject(CreateProjectDto dto)
        {
            var result = await _projectService.CreateProjectAsync(dto);
            if (!result.Success) return BadRequest(result);
            return CreatedAtAction(nameof(GetProject), new { projectId = result.Data.Id }, result);
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<ProjectDto>>>> GetProjects(
            [FromQuery] PaginationDto pagination,
            [FromQuery] string? name = null,
            [FromQuery] Guid? customerId = null)
        {
            var result = await _projectService.GetProjectsAsync(pagination, name, customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{projectId}")]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> GetProject(Guid projectId)
        {
            var result = await _projectService.GetProjectAsync(projectId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpPut("{projectId}")]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> UpdateProject(Guid projectId, UpdateProjectDto dto)
        {
            var result = await _projectService.UpdateProjectAsync(projectId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{projectId}/status")]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> UpdateProjectStatus(Guid projectId, [FromBody] UpdateProjectStatusDto dto)
        {
            var result = await _projectService.UpdateProjectStatusAsync(projectId, dto.Status);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{projectId}")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteProject(Guid projectId)
        {
            var result = await _projectService.DeleteProjectAsync(projectId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}
