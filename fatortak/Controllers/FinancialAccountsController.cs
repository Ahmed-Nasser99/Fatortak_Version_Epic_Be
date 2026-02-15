using fatortak.Dtos;
using fatortak.Dtos.Shared;
using fatortak.Services.FinancialAccountService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/financial-accounts")]
    [ApiController]
    [Authorize]
    public class FinancialAccountsController : ControllerBase
    {
        private readonly IFinancialAccountService _service;

        public FinancialAccountsController(IFinancialAccountService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<FinancialAccountDto>>> CreateAccount(CreateFinancialAccountDto dto)
        {
            var result = await _service.CreateAccountAsync(dto);
            if (!result.Success) return BadRequest(result);
            return CreatedAtAction(nameof(GetAccount), new { accountId = result.Data.Id }, result);
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<FinancialAccountDto>>>> GetAccounts(
            [FromQuery] PaginationDto pagination,
            [FromQuery] string? name = null)
        {
            var result = await _service.GetAccountsAsync(pagination, name);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{accountId}")]
        public async Task<ActionResult<ServiceResult<FinancialAccountDto>>> GetAccount(Guid accountId)
        {
            var result = await _service.GetAccountAsync(accountId);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpPut("{accountId}")]
        public async Task<ActionResult<ServiceResult<FinancialAccountDto>>> UpdateAccount(Guid accountId, UpdateFinancialAccountDto dto)
        {
            var result = await _service.UpdateAccountAsync(accountId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{accountId}")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteAccount(Guid accountId)
        {
             var result = await _service.DeleteAccountAsync(accountId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}
