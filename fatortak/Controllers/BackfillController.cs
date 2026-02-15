using fatortak.Services.BackfillService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Authorize]
    [Route("api/backfill")]
    [ApiController]
    public class BackfillController : ControllerBase
    {
        private readonly IBackfillService _backfillService;

        public BackfillController(IBackfillService backfillService)
        {
            _backfillService = backfillService;
        }

        [HttpPost("transactions")]
        public async Task<IActionResult> BackfillTransactions()
        {
            await _backfillService.BackfillTransactionsAsync();
            return Ok(new { message = "Transactions backfill completed successfully" });
        }

        [HttpPost("branches")]
        public async Task<IActionResult> BackfillBranches([FromQuery] Guid? tenantId)
        {
            await _backfillService.BackfillBranchesAsync(tenantId);
            return Ok(new { message = "Branches backfill completed successfully" });
        }
    }
}
