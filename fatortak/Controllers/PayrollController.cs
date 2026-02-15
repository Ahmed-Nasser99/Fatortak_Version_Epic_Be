using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Services.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers.HR
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PayrollController : ControllerBase
    {
        private readonly IPayrollService _service;
        private readonly ILogger<PayrollController> _logger;

        public PayrollController(IPayrollService service, ILogger<PayrollController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost("generate")]
        public async Task<ActionResult<ServiceResult<PayrollDto>>> Generate([FromBody] GeneratePayrollDto dto)
        {
            var result = await _service.GeneratePayrollAsync(dto);
            return HandleServiceResult(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceResult<PayrollDto>>> GetById(Guid id)
        {
            var result = await _service.GetPayrollAsync(id);
            return HandleServiceResult(result);
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<List<PayrollDto>>>> GetAll()
        {
            var result = await _service.GetAllPayrollsAsync();
            return HandleServiceResult(result);
        }

        [HttpPost("{id}/submit")]
        public async Task<ActionResult<ServiceResult<PayrollDto>>> Submit(Guid id)
        {
            var result = await _service.SubmitPayrollAsync(id);
            return HandleServiceResult(result);
        }

        [HttpPost("{id}")]
        public async Task<ActionResult<ServiceResult<bool>>> Delete(Guid id)
        {
            var result = await _service.DeletePayrollAsync(id);
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
