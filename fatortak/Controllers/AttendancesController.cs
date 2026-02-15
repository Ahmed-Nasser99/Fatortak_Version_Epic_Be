using fatortak.Dtos.HR.Attendance;
using fatortak.Dtos.Shared;
using fatortak.Services.HR.AttendanceService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AttendancesController : ControllerBase
    {
        private readonly IAttendanceService _service;
        private readonly ILogger<AttendancesController> _logger;

        public AttendancesController(IAttendanceService service, ILogger<AttendancesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<AttendanceDto>>>> GetAll([FromQuery] PaginationDto pagination,[FromQuery] AttendanceFiltersDto filters)
        {
            var result = await _service.GetAllAttendancesAsync(pagination , filters);
            return HandleServiceResult(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceResult<AttendanceDto>>> GetById(Guid id)
        {
            var result = await _service.GetAttendanceByIdAsync(id);
            return HandleServiceResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<AttendanceDto>>> Create([FromBody] CreateAttendanceDto dto)
        {
            var result = await _service.CreateAttendanceAsync(dto);
            return HandleServiceResult(result);
        }

        [HttpPost("update/{id}")]
        public async Task<ActionResult<ServiceResult<AttendanceDto>>> Update(Guid id, [FromBody] UpdateAttendanceDto dto)
        {
            var result = await _service.UpdateAttendanceAsync(id, dto);
            return HandleServiceResult(result);
        }

        [HttpPost("delete/{id}")]
        public async Task<ActionResult<ServiceResult<bool>>> Delete(Guid id)
        {
            var result = await _service.DeleteAttendanceAsync(id);
            return HandleServiceResult(result);
        }
        [HttpGet("daily-report/{date}")]
        public async Task<ActionResult<ServiceResult<List<DailyAttendanceReportDto>>>> GetDailyReport(DateOnly date)
        {
            var result = await _service.GetDailyAttendanceReportAsync(date);
            return HandleServiceResult(result);
        }

        [HttpGet("monthly-report/{year}/{month}")]
        public async Task<ActionResult<ServiceResult<List<MonthlyAttendanceReportDto>>>> GetMonthlyReport(int year, int month)
        {
            var result = await _service.GetMonthlyAttendanceReportAsync(year, month);
            return HandleServiceResult(result);
        }
        [HttpGet("daily-report/{date}/export/excel")]
        public async Task<IActionResult> ExportDailyReportToExcel(DateOnly date)
        {
            try
            {
                var fileBytes = await _service.ExportDailyAttendanceToExcelAsync(date);
                var fileName = $"DailyAttendanceReport_{date:yyyy-MM-dd}.xlsx";

                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting daily report to Excel for date: {Date}", date);
                return StatusCode(500, "An error occurred while exporting the report");
            }
        }

        [HttpGet("daily-report/{date}/export/pdf")]
        public async Task<IActionResult> ExportDailyReportToPdf(DateOnly date)
        {
            try
            {
                var fileBytes = await _service.ExportDailyAttendanceToPdfAsync(date);
                var fileName = $"DailyAttendanceReport_{date:yyyy-MM-dd}.pdf";

                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting daily report to PDF for date: {Date}", date);
                return StatusCode(500, "An error occurred while exporting the report");
            }
        }

        [HttpGet("monthly-report/{year}/{month}/export/excel")]
        public async Task<IActionResult> ExportMonthlyReportToExcel(int year, int month)
        {
            try
            {
                var fileBytes = await _service.ExportMonthlyAttendanceToExcelAsync(year, month);
                var fileName = $"MonthlyAttendanceReport_{year}-{month:D2}.xlsx";

                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting monthly report to Excel for year: {Year}, month: {Month}", year, month);
                return StatusCode(500, "An error occurred while exporting the report");
            }
        }

        [HttpGet("monthly-report/{year}/{month}/export/pdf")]
        public async Task<IActionResult> ExportMonthlyReportToPdf(int year, int month)
        {
            try
            {
                var fileBytes = await _service.ExportMonthlyAttendanceToPdfAsync(year, month);
                var fileName = $"MonthlyAttendanceReport_{year}-{month:D2}.pdf";

                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting monthly report to PDF for year: {Year}, month: {Month}", year, month);
                return StatusCode(500, "An error occurred while exporting the report");
            }
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
