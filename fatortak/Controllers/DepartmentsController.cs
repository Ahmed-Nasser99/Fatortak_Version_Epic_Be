using fatortak.Dtos.HR.Departments;
using fatortak.Dtos.Shared;
using fatortak.Services.HR.DepartmentService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers.HR
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DepartmentsController : ControllerBase
    {
        private readonly IDepartmentService _service;
        private readonly ILogger<DepartmentsController> _logger;

        public DepartmentsController(IDepartmentService service, ILogger<DepartmentsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<DepartmentDto>>>> GetAll([FromQuery] PaginationDto pagination, [FromQuery] DepartmentFilterDto filter)
        {
            var result = await _service.GetAllDepartmentsAsync(pagination, filter);
            return HandleServiceResult(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceResult<DepartmentDto>>> GetById(Guid id)
        {
            var result = await _service.GetDepartmentByIdAsync(id);
            return HandleServiceResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<DepartmentDto>>> Create([FromBody] CreateDepartmentDto dto)
        {
            var result = await _service.CreateDepartmentAsync(dto);
            return HandleServiceResult(result);
        }

        [HttpPost("update/{id}")]
        public async Task<ActionResult<ServiceResult<DepartmentDto>>> Update(Guid id, [FromBody] UpdateDepartmentDto dto)
        {
            var result = await _service.UpdateDepartmentAsync(id, dto);
            return HandleServiceResult(result);
        }

        [HttpPost("delete/{id}")]
        public async Task<ActionResult<ServiceResult<bool>>> Delete(Guid id)
        {
            var result = await _service.DeleteDepartmentAsync(id);
            return HandleServiceResult(result);
        }

        private ActionResult HandleServiceResult<T>(ServiceResult<T> result)
        {
            if (result.Success)
                return Ok(result);

            if (result.Errors != null && result.Errors.Any())
            {
                _logger.LogWarning("Validation errors: {Errors}", string.Join(", ", result.Errors));
                return BadRequest(result);
            }

            _logger.LogError("Service error: {ErrorMessage}", result.ErrorMessage);
            return StatusCode(500, result);
        }
    }
}
