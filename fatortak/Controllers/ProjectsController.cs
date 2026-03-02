using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Dtos.Project;
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
        private readonly IProjectExportService _projectExportService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(
            IProjectService projectService, 
            IProjectExportService projectExportService,
            ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _projectExportService = projectExportService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> CreateProject([FromBody] CreateProjectDto dto)
        {
            var result = await _projectService.CreateProjectAsync(dto);
            if (!result.Success) return BadRequest(result);
            return CreatedAtAction(nameof(GetProject), new { projectId = result.Data.Id }, result);
        }

        [HttpPost("with-contract")]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> CreateProjectWithContract([FromBody] CreateProjectWithContractCommand command)
        {
            var result = await _projectService.CreateProjectWithContractAsync(command);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
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

        [HttpPost("{projectId}/update")]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> UpdateProject(Guid projectId, [FromBody] UpdateProjectDto dto)
        {
            var result = await _projectService.UpdateProjectAsync(projectId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{projectId}/update-with-contract")]
        public async Task<ActionResult<ServiceResult<ProjectDto>>> UpdateProjectWithContract(Guid projectId, [FromBody] UpdateProjectWithContractCommand command)
        {
            var result = await _projectService.UpdateProjectWithContractAsync(projectId, command);
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

        [HttpPost("{projectId}/delete")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteProject(Guid projectId)
        {
            var result = await _projectService.DeleteProjectAsync(projectId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{projectId}/export/pdf")]
        public async Task<IActionResult> ExportProjectPdf(Guid projectId)
        {
            try
            {
                var content = await _projectExportService.ExportProjectToPdfAsync(projectId);
                if (content == null) return NotFound("Project not found");
                return File(content, "application/pdf", $"Quotation_{projectId}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting project to PDF");
                return BadRequest("Failed to generate PDF");
            }
        }

        [HttpGet("{projectId}/export/excel")]
        public async Task<IActionResult> ExportProjectExcel(Guid projectId)
        {
            try
            {
                var content = await _projectExportService.ExportProjectToExcelAsync(projectId);
                if (content == null) return NotFound("Project not found");
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Project_{projectId}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting project to Excel");
                return BadRequest("Failed to generate Excel");
            }
        }
    }
}
