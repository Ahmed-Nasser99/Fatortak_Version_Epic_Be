using fatortak.Services.DashboardService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData([FromQuery]string period = "month", [FromQuery] Guid? branchId = null)
        {
            try
            {
                var dashboardData = await _dashboardService.GetDashboardDataAsync(period, branchId);
                return Ok(new { success = true, data = dashboardData });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
