using fatortak.Dtos;
using fatortak.Services.ExpenseCategoryService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExpenseCategoryController : ControllerBase
    {
        private readonly IExpenseCategoryService _expenseCategoryService;

        public ExpenseCategoryController(IExpenseCategoryService expenseCategoryService)
        {
            _expenseCategoryService = expenseCategoryService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExpenseCategoryDto>>> GetAll()
        {
            var categories = await _expenseCategoryService.GetAllAsync();
            return Ok(categories);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ExpenseCategoryDto>> GetById(Guid id)
        {
            var category = await _expenseCategoryService.GetByIdAsync(id);
            if (category == null) return NotFound();
            return Ok(category);
        }

        [HttpPost]
        public async Task<ActionResult<ExpenseCategoryDto>> Create(CreateExpenseCategoryDto dto)
        {
            var category = await _expenseCategoryService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
        }

        [HttpPost("update/{id}")]
        public async Task<IActionResult> Update(Guid id, UpdateExpenseCategoryDto dto)
        {
            var success = await _expenseCategoryService.UpdateAsync(id, dto);
            if (!success) return NotFound();
            return Ok(new { message = "Update successful" });
        }

        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var success = await _expenseCategoryService.DeleteAsync(id);
            if (!success) return NotFound();
            return Ok(new { message = "Delete successful" });
        }
    }
}
