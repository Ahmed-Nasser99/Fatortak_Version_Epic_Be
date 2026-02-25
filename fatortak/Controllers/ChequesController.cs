using fatortak.Dtos.Cheque;
using fatortak.Dtos.Shared;
using fatortak.Services.ChequeService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChequesController : ControllerBase
    {
        private readonly IChequeService _chequeService;
        private readonly ILogger<ChequesController> _logger;

        public ChequesController(IChequeService chequeService, ILogger<ChequesController> logger)
        {
            _chequeService = chequeService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<ChequeDto>>>> GetCheques(
            [FromQuery] PaginationDto pagination,
            [FromQuery] string? status = null)
        {
            try
            {
                var result = await _chequeService.GetChequesAsync(pagination, status);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cheques");
                return BadRequest(ServiceResult<PagedResponseDto<ChequeDto>>.Failure("Failed to retrieve cheques"));
            }
        }

        [HttpPost("{chequeId}/status")]
        public async Task<ActionResult<ServiceResult<ChequeDto>>> UpdateChequeStatus(
            Guid chequeId,
            [FromBody] UpdateChequeStatusDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<ChequeDto>.ValidationError(errors));
                }

                var result = await _chequeService.UpdateChequeStatusAsync(chequeId, dto);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "Cheque not found")
                    {
                        return NotFound(result);
                    }
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for cheque with ID: {chequeId}");
                return BadRequest(ServiceResult<ChequeDto>.Failure("Failed to update cheque status: " + ex.Message));
            }
        }
    }
}
