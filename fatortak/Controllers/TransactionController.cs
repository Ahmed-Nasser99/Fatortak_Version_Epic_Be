using fatortak.Dtos.Shared;
using fatortak.Dtos.Transaction;
using fatortak.Services.TransactionService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Authorize]
    [Route("api/transactions")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<TransactionDto>>>> GetTransactions(
            [FromQuery] TransactionFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            var result = await _transactionService.GetTransactionsAsync(filter, pagination);
            
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            var transactionDtos = result.Data.Data.Select(t => new TransactionDto
            {
                Id = t.Id,
                TransactionDate = t.TransactionDate,
                Type = t.Type,
                Amount = t.Amount,
                Direction = t.Direction,
                ReferenceId = t.ReferenceId,
                ReferenceType = t.ReferenceType ?? "",
                Description = t.Description ?? "",
                PaymentMethod = t.PaymentMethod ?? "",
                CreatedBy = t.CreatedBy ?? Guid.Empty,
                CreatedAt = t.CreatedAt
            }).ToList();

            var response = new PagedResponseDto<TransactionDto>
            {
                Data = transactionDtos,
                PageNumber = result.Data.PageNumber,
                PageSize = result.Data.PageSize,
                TotalCount = result.Data.TotalCount
            };

            return Ok(ServiceResult<PagedResponseDto<TransactionDto>>.SuccessResult(response));
        }

        [HttpGet("balance")]
        public async Task<ActionResult<ServiceResult<decimal>>> GetBalance()
        {
            return Ok(await _transactionService.GetBalanceAsync());
        }
    }
}
