using fatortak.Dtos.HR.Employee;
using fatortak.Dtos.Shared;
using fatortak.Services.HR.EmployeeService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers.HR
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly IEmployeeService _service;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(IEmployeeService service, ILogger<EmployeesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<EmployeeDto>>>> GetAll([FromQuery] PaginationDto pagination, [FromQuery]EmployeeFilterDto filter)
        {
            var result = await _service.GetAllEmployeesAsync(pagination , filter);
            return HandleServiceResult(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceResult<EmployeeDto>>> GetById(Guid id)
        {
            var result = await _service.GetEmployeeByIdAsync(id);
            return HandleServiceResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<EmployeeDto>>> Create([FromBody] CreateEmployeeDto dto)
        {
            var result = await _service.CreateEmployeeAsync(dto);
            return HandleServiceResult(result);
        }

        [HttpPost("update/{id}")]
        public async Task<ActionResult<ServiceResult<EmployeeDto>>> Update(Guid id, [FromBody] UpdateEmployeeDto dto)
        {
            var result = await _service.UpdateEmployeeAsync(id, dto);
            return HandleServiceResult(result);
        }

        [HttpPost("delete/{id}")]
        public async Task<ActionResult<ServiceResult<bool>>> Delete(Guid id)
        {
            var result = await _service.DeleteEmployeeAsync(id);
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
