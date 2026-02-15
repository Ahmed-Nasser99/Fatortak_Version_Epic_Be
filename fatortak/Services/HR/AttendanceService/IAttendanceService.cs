using fatortak.Dtos.HR.Attendance;
using fatortak.Dtos.Shared;

namespace fatortak.Services.HR.AttendanceService
{
    public interface IAttendanceService
    {
        Task<ServiceResult<PagedResponseDto<AttendanceDto>>> GetAllAttendancesAsync(PaginationDto pagination, AttendanceFiltersDto filters);
        Task<ServiceResult<AttendanceDto>> GetAttendanceByIdAsync(Guid id);
        Task<ServiceResult<AttendanceDto>> CreateAttendanceAsync(CreateAttendanceDto dto);
        Task<ServiceResult<AttendanceDto>> UpdateAttendanceAsync(Guid id, UpdateAttendanceDto dto);
        Task<ServiceResult<bool>> DeleteAttendanceAsync(Guid id);

        Task<ServiceResult<List<DailyAttendanceReportDto>>> GetDailyAttendanceReportAsync(DateOnly date);
        Task<ServiceResult<List<MonthlyAttendanceReportDto>>> GetMonthlyAttendanceReportAsync(int year, int month);
        Task<byte[]> ExportDailyAttendanceToExcelAsync(DateOnly date);
        Task<byte[]> ExportMonthlyAttendanceToExcelAsync(int year, int month);
        Task<byte[]> ExportDailyAttendanceToPdfAsync(DateOnly date);
        Task<byte[]> ExportMonthlyAttendanceToPdfAsync(int year, int month);
    }
}
