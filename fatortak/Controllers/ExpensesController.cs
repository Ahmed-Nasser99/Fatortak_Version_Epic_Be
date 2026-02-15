using fatortak.Dtos.Expense;
using fatortak.Dtos.Shared;
using fatortak.Services.ExpenseService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;
        private readonly ILogger<ExpensesController> _logger;

        public ExpensesController(
            IExpenseService expenseService,
            ILogger<ExpensesController> logger)
        {
            _expenseService = expenseService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<ExpenseDto>>>> GetAll([FromQuery] ExpenseFilterDto filter, [FromQuery] PaginationDto pagination)
        {
            var result = await _expenseService.GetAllExpensesAsync(filter, pagination);
            return HandleServiceResult(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceResult<ExpenseDto>>> GetById(int id)
        {
            var result = await _expenseService.GetExpenseByIdAsync(id);
            return HandleServiceResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<ExpenseDto>>> Create([FromForm] CreateExpenseDto dto)
        {
            var result = await _expenseService.CreateExpenseAsync(dto);
            return HandleServiceResult(result);
        }

        [HttpPost("update/{id}")]
        public async Task<ActionResult<ServiceResult<ExpenseDto>>> Update(int id, [FromForm] UpdateExpenseDto dto)
        {
            var result = await _expenseService.UpdateExpenseAsync(id, dto);
            return HandleServiceResult(result);
        }

        [HttpPost("delete/{id}")]
        public async Task<ActionResult<ServiceResult<bool>>> Delete(int id)
        {
            var result = await _expenseService.DeleteExpenseAsync(id);
            return HandleServiceResult(result);
        }

        private ActionResult HandleServiceResult<T>(ServiceResult<T> result)
        {
            if (result.Success)
            {
                return Ok(result);
            }

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
