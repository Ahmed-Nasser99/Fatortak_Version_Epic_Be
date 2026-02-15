using fatortak.Dtos;
using fatortak.Services.BranchService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : ControllerBase
    {
        private readonly IBranchService _branchService;

        public BranchesController(IBranchService branchService)
        {
            _branchService = branchService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateBranch([FromBody] CreateBranchDto dto)
        {
            var result = await _branchService.CreateBranchAsync(dto);
            if (!result.Success)
                return BadRequest(result.ErrorMessage);
            return Ok(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var result = await _branchService.GetBranchesAsync();
            if (!result.Success)
                return BadRequest(result.ErrorMessage);
            return Ok(result.Data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBranch(Guid id)
        {
            var result = await _branchService.GetBranchAsync(id);
            if (!result.Success)
                return NotFound(result.ErrorMessage);
            return Ok(result.Data);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateBranchDto dto)
        {
            var result = await _branchService.UpdateBranchAsync(id, dto);
            if (!result.Success)
                return BadRequest(result.ErrorMessage);
            return Ok(result.Data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBranch(Guid id)
        {
            var result = await _branchService.DeleteBranchAsync(id);
            if (!result.Success)
                return BadRequest(result.ErrorMessage);
            return Ok(result.Data);
        }

        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleActivation(Guid id)
        {
            var result = await _branchService.ToggleActivationAsync(id);
            if (!result.Success)
                return BadRequest(result.ErrorMessage);
            return Ok(result.Data);
        }

        [HttpGet("main")]
        public async Task<IActionResult> GetMainBranch()
        {
            var result = await _branchService.GetMainBranchAsync();
            if (!result.Success)
                return NotFound(result.ErrorMessage);
            return Ok(result.Data);
        }
    }
}
