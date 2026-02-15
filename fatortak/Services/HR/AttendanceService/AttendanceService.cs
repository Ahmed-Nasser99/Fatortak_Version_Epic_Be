using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.HR.Attendance;
using fatortak.Dtos.Shared;
using fatortak.Entities;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace fatortak.Services.HR.AttendanceService
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AttendanceService> _logger;

        public AttendanceService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AttendanceService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private Guid _tenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<PagedResponseDto<AttendanceDto>>> GetAllAttendancesAsync(PaginationDto pagination , AttendanceFiltersDto filters)
        {
            try
            {
                var query = _context.Attendances
                    .Include(a => a.Employee)
                    .Where(a => a.TenantId == _tenantId);

                if (filters.date.HasValue)
                {
                    var filterDateTime = filters.date.Value.ToDateTime(TimeOnly.MinValue);
                    query = query.Where(a => a.Date.Date == filterDateTime.Date);
                }
                else
                {
                    query = query.Where(a => a.Date.Date == DateTime.UtcNow.Date);
                }


                var totalCount = await query.CountAsync();

                var attendances = await query
                    .OrderByDescending(a => a.Date)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .Select(a => new AttendanceDto
                    {
                        Id = a.Id,
                        EmployeeId = a.EmployeeId,
                        EmployeeName = a.Employee.FullName,
                        AttendanceDate = a.Date,
                        AttendTime = a.AttendTime,
                        LeaveTime = a.LeaveTime,
                        Status = a.Status ?? string.Empty,
                        Reason = a.Reason
                    })
                    .ToListAsync();

                return ServiceResult<PagedResponseDto<AttendanceDto>>.SuccessResult(new PagedResponseDto<AttendanceDto>
                {
                    Data = attendances,
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attendances");
                return ServiceResult<PagedResponseDto<AttendanceDto>>.Failure("Failed to load attendances");
            }
        }

        public async Task<ServiceResult<AttendanceDto>> GetAttendanceByIdAsync(Guid id)
        {
            try
            {
                var attendance = await _context.Attendances
                    .Include(a => a.Employee)
                    .Where(a => a.TenantId == _tenantId && a.Id == id)
                    .Select(a => new AttendanceDto
                    {
                        Id = a.Id,
                        EmployeeId = a.EmployeeId,
                        AttendanceDate = a.Date,
                        AttendTime = a.AttendTime,
                        LeaveTime = a.LeaveTime,
                        Status = a.Status.ToString(),
                        Reason = a.Reason
                    })
                    .FirstOrDefaultAsync();

                return attendance == null
                    ? ServiceResult<AttendanceDto>.Failure("Attendance not found")
                    : ServiceResult<AttendanceDto>.SuccessResult(attendance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting attendance by ID");
                return ServiceResult<AttendanceDto>.Failure("Failed to load attendance");
            }
        }

        public async Task<ServiceResult<AttendanceDto>> CreateAttendanceAsync(CreateAttendanceDto dto)
        {
            try
            {
                var attendance = new Attendance
                {
                    EmployeeId = dto.EmployeeId,
                    Date = dto.AttendanceDate,
                    AttendTime = dto.AttendTime,
                    LeaveTime = dto.LeaveTime,
                    Status = dto.Status,
                    Reason = dto.Reason,
                    TenantId = _tenantId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Attendances.Add(attendance);
                await _context.SaveChangesAsync();

                return ServiceResult<AttendanceDto>.SuccessResult(new AttendanceDto
                {
                    Id = attendance.Id,
                    EmployeeId = attendance.EmployeeId,
                    AttendanceDate = attendance.Date,
                    AttendTime = attendance.AttendTime,
                    LeaveTime = attendance.LeaveTime,
                    Status = attendance.Status.ToString(),
                    Reason = attendance.Reason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance");
                return ServiceResult<AttendanceDto>.Failure("Failed to create attendance");
            }
        }

        public async Task<ServiceResult<AttendanceDto>> UpdateAttendanceAsync(Guid id, UpdateAttendanceDto dto)
        {
            try
            {
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.TenantId == _tenantId && a.Id == id);

                if (attendance == null)
                    return ServiceResult<AttendanceDto>.Failure("Attendance not found");

                if (dto.AttendanceDate.HasValue)
                    attendance.Date = dto.AttendanceDate.Value;
                
                
                if (dto.AttendTime.HasValue)
                    attendance.AttendTime = dto.AttendTime.Value;

                if (dto.LeaveTime.HasValue)
                    attendance.LeaveTime = dto.LeaveTime.Value;

                if (!string.IsNullOrEmpty(dto.Status))
                    attendance.Status = dto.Status;

                if (!string.IsNullOrWhiteSpace(dto.Reason))
                    attendance.Reason = dto.Reason;

                attendance.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return ServiceResult<AttendanceDto>.SuccessResult(new AttendanceDto
                {
                    Id = attendance.Id,
                    EmployeeId = attendance.EmployeeId,
                    AttendanceDate = attendance.Date,
                    AttendTime = attendance.AttendTime,
                    LeaveTime = attendance.LeaveTime,
                    Status = attendance.Status.ToString(),
                    Reason = attendance.Reason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendance");
                return ServiceResult<AttendanceDto>.Failure("Failed to update attendance");
            }
        }

        public async Task<ServiceResult<bool>> DeleteAttendanceAsync(Guid id)
        {
            try
            {
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.TenantId == _tenantId && a.Id == id);

                if (attendance == null)
                    return ServiceResult<bool>.Failure("Attendance not found");

                _context.Attendances.Remove(attendance);
                await _context.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance");
                return ServiceResult<bool>.Failure("Failed to delete attendance");
            }
        }

        public async Task<ServiceResult<List<DailyAttendanceReportDto>>> GetDailyAttendanceReportAsync(DateOnly date)
        {
            try
            {
                var workSetting = await _context.WorkSettings
                    .FirstOrDefaultAsync(w => w.TenantId == _tenantId);

                if (workSetting == null)
                {
                    return ServiceResult<List<DailyAttendanceReportDto>>.Failure("Work settings not found");
                }

                // Get all active employees
                var allEmployees = await _context.Employees
                    .Include(e => e.Department)
                    .Where(e => e.TenantId == _tenantId)
                    .ToListAsync();

                // Get attendances for the specific date
                var attendances = await _context.Attendances
                    .Where(a => a.TenantId == _tenantId && a.Date.Date == date.ToDateTime(TimeOnly.MinValue))
                    .ToListAsync();

                // Create report for all employees
                var report = allEmployees.Select(employee =>
                {
                    // Find attendance for this employee on the given date
                    var attendance = attendances.FirstOrDefault(a => a.EmployeeId == employee.Id);

                    if (attendance == null)
                    {
                        // Employee is absent
                        return new DailyAttendanceReportDto
                        {
                            Date = date,
                            EmployeeName = employee.FullName,
                            Department = employee.Department?.Name ?? "N/A",
                            AttendanceTime = null,
                            DepartureTime = null,
                            DelayDurationHours = TimeSpan.Zero,
                            TotalWorkingHours = TimeSpan.Zero,
                            IsAttended = false
                        };
                    }


                    if (attendance.Status != null && attendance.Status.ToString() == AttendanceStatus.LeaveWithReason.ToString())
                    {
                        // Employee is absent
                        return new DailyAttendanceReportDto
                        {
                            Date = date,
                            EmployeeName = employee.FullName,
                            Department = employee.Department?.Name ?? "N/A",
                            AttendanceTime = null,
                            DepartureTime = null,
                            DelayDurationHours = TimeSpan.Zero,
                            TotalWorkingHours = TimeSpan.Zero,
                            IsAttended = false,
                            IsVacation = true
                        };
                    }

                    // Employee attended - calculate times
                    var attTime = attendance.AttendTime?.TimeOfDay ?? TimeSpan.Zero;
                    var leaveTime = attendance.LeaveTime?.TimeOfDay ?? TimeSpan.Zero;

                    // Calculate delay
                    var delay = attTime > workSetting.WorkStartTime.Add(TimeSpan.FromMinutes(workSetting.GracePeriodMinutes))
                        ? attTime - workSetting.WorkStartTime
                        : TimeSpan.Zero;

                    // Calculate total working
                    var totalWorking = (attendance.LeaveTime.HasValue && attendance.AttendTime.HasValue)
                        ? attendance.LeaveTime.Value - attendance.AttendTime.Value
                        : TimeSpan.Zero;

                    // Calculate excess break (if more than allowed break time)
                    var excessBreak = totalWorking < (workSetting.WorkEndTime - workSetting.WorkStartTime).Subtract(TimeSpan.FromMinutes(workSetting.BreakMinutes))
                        ? ((workSetting.WorkEndTime - workSetting.WorkStartTime) - totalWorking)
                        : TimeSpan.Zero;

                    return new DailyAttendanceReportDto
                    {
                        Date = date,
                        EmployeeName = employee.FullName,
                        Department = employee.Department?.Name ?? "N/A",
                        AttendanceTime = attTime,
                        DepartureTime = leaveTime,
                        DelayDurationHours = delay,
                        TotalWorkingHours = totalWorking,
                        IsAttended = attendance.AttendTime.HasValue
                    };
                }).ToList();

                report = report.OrderByDescending(r => r.AttendanceTime).ToList();

                return ServiceResult<List<DailyAttendanceReportDto>>.SuccessResult(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily report");
                return ServiceResult<List<DailyAttendanceReportDto>>.Failure("Failed to generate daily report");
            }
        }

        public async Task<ServiceResult<List<MonthlyAttendanceReportDto>>> GetMonthlyAttendanceReportAsync(int year, int month)
        {
            try
            {
                var workSetting = await _context.WorkSettings
                    .FirstOrDefaultAsync(w => w.TenantId == _tenantId);

                if (workSetting == null)
                {
                    return ServiceResult<List<MonthlyAttendanceReportDto>>.Failure("Work settings not found");
                }

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Get ALL employees
                var allEmployees = await _context.Employees
                    .Include(e => e.Department)
                    .Where(e => e.TenantId == _tenantId)
                    .ToListAsync();

                // Get attendance records for the period
                var attendances = await _context.Attendances
                    .Include(a => a.Employee)
                    .Where(a => a.TenantId == _tenantId && a.Date >= startDate && a.Date <= endDate)
                    .ToListAsync();

                var weekendDays = workSetting.WeekendDays.Split(',')
                    .Select(day => int.TryParse(day.Trim(), out int result) ? result : -1)
                    .Where(day => day >= 0 && day <= 6)
                    .ToList();

                if (!weekendDays.Any())
                {
                    weekendDays = new List<int> { 0, 6 };
                }

                int workingDaysInMonth = CalculateWorkingDaysInMonth(year, month, weekendDays);
                var dailyWorkingHours = (workSetting.WorkEndTime - workSetting.WorkStartTime).TotalHours;
                var expectedWorkingHours = workingDaysInMonth * dailyWorkingHours;

                // Create report for ALL employees
                var report = allEmployees.Select(employee =>
                {
                    // Get attendance records for this specific employee
                    var employeeAttendances = attendances
                        .Where(a => a.EmployeeId == employee.Id)
                        .ToList();

                    var totalWorking = TimeSpan.Zero;
                    var totalDelay = TimeSpan.Zero;
                    var totalOvertime = TimeSpan.Zero;
                    var delayDays = 0;
                    var presentDays = 0;
                    var absentDays = 0;
                    var overtimeDays = 0;
                    var vacationDays = 0;

                    foreach (var attendance in employeeAttendances)
                    {
                        var attTime = attendance.AttendTime?.TimeOfDay ?? TimeSpan.Zero;
                        var leaveTime = attendance.LeaveTime?.TimeOfDay ?? TimeSpan.Zero;

                        if (attendance.Status != null && attendance.Status.ToLower() == AttendanceStatus.LeaveWithReason.ToString().ToLower())
                        {
                            var dayWorking = workSetting.WorkEndTime - workSetting.WorkStartTime;
                            totalWorking += dayWorking;
                            vacationDays++;
                        }
                        else if (attendance.AttendTime.HasValue && attendance.LeaveTime.HasValue)
                        {
                            var dayWorking = attendance.LeaveTime.Value - attendance.AttendTime.Value;
                            totalWorking += dayWorking;
                            presentDays++;

                            // Check for delay
                            if (attTime > workSetting.WorkStartTime.Add(TimeSpan.FromMinutes(workSetting.GracePeriodMinutes)))
                            {
                                totalDelay += (attTime - workSetting.WorkStartTime);
                                delayDays++;
                            }

                            // Check for overtime (working beyond regular work end time)
                            if (dayWorking > (workSetting.WorkEndTime - workSetting.WorkStartTime))
                            {
                                var overtime = dayWorking - (workSetting.WorkEndTime - workSetting.WorkStartTime);
                                totalOvertime += overtime;
                                overtimeDays++;
                            }
                        }
                    }

                    // Calculate absent days (working days minus present days)
                    absentDays = workingDaysInMonth - (presentDays + vacationDays);

                    decimal? calculatedSalary = null;
                    if (employee.Salary.HasValue && expectedWorkingHours > 0)
                    {
                        calculatedSalary = employee.Salary.Value *
                                           (decimal)(totalWorking.TotalHours / expectedWorkingHours);
                    }

                    return new MonthlyAttendanceReportDto
                    {
                        EmployeeId = employee.Id,
                        EmployeeName = employee.FullName,
                        Department = employee.Department?.Name ?? "N/A",
                        TotalMonthlyWorkingHours = $"{(int)totalWorking.TotalHours}:{totalWorking.Minutes:D2}",
                        TotalDelayHours = $"{(int)totalDelay.TotalHours}:{totalDelay.Minutes:D2}",
                        TotalOvertimeHours = $"{(int)totalOvertime.TotalHours}:{totalOvertime.Minutes:D2}",
                        NumberOfDelayDays = delayDays,
                        NumberOfOvertimeDays = overtimeDays,
                        ExpectedMonthlyWorkingHours = $"{(int)expectedWorkingHours}:{(int)((expectedWorkingHours - Math.Floor(expectedWorkingHours)) * 60):D2}",
                        PresentDays = presentDays,
                        AbsentDays = absentDays,
                        VacationDays = vacationDays,
                        Salary = calculatedSalary.HasValue
                                ? Math.Round(calculatedSalary.Value, 2)
                                : (decimal?)null,
                        ExpectedSalary = employee.Salary
                    };
                }).ToList();

                return ServiceResult<List<MonthlyAttendanceReportDto>>.SuccessResult(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly report");
                return ServiceResult<List<MonthlyAttendanceReportDto>>.Failure("Failed to generate monthly report");
            }
        }

        public async Task<byte[]> ExportDailyAttendanceToExcelAsync(DateOnly date)
        {
            var reportResult = await GetDailyAttendanceReportAsync(date);
            if (!reportResult.Success)
                throw new InvalidOperationException(reportResult.ErrorMessage);

            var data = reportResult.Data;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add($"Daily Report {date:yyyy-MM-dd}");

                // Set the worksheet direction to Right-to-Left
                worksheet.View.RightToLeft = true;

                // Report title and date
                worksheet.Cells[1, 1].Value = "تقرير الحضور اليومي";
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1, 1, 9].Merge = true;

                worksheet.Cells[2, 1].Value = $"تاريخ التقرير: {date:yyyy-MM-dd}";
                worksheet.Cells[2, 1].Style.Font.Bold = true;
                worksheet.Cells[2, 1, 2, 9].Merge = true;

                // Headers
                var startRow = 4;
                var headers = new string[]
                {
                    "اسم الموظف",
                    "القسم",
                    "وقت الحضور",
                    "وقت الانصراف",
                    "مدة التأخير",
                    "ساعات العمل الكلية",
                    "حالة الحضور",
                    "في إجازة"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[startRow, i + 1].Value = headers[i];
                }

                // Style headers
                using (var headerRange = worksheet.Cells[startRow, 1, startRow, headers.Length])
                {
                    headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    headerRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    headerRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    headerRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                }

                // Data rows
                for (int row = 0; row < data.Count; row++)
                {
                    var item = data[row];
                    var currentRow = row + startRow + 1;

                    worksheet.Cells[currentRow, 1].Value = item.EmployeeName;
                    worksheet.Cells[currentRow, 2].Value = item.Department;
                    worksheet.Cells[currentRow, 3].Value = item.AttendanceTime?.ToString() ?? "";
                    worksheet.Cells[currentRow, 4].Value = item.DepartureTime?.ToString() ?? "";
                    worksheet.Cells[currentRow, 5].Value = item.DelayDurationHours.ToString();
                    worksheet.Cells[currentRow, 6].Value = item.TotalWorkingHours.ToString();
                    worksheet.Cells[currentRow, 7].Value = item.IsAttended ? "حاضر" : "غائب";
                    worksheet.Cells[currentRow, 8].Value = item.IsVacation ? "نعم" : "لا";

                    // Color coding for attendance status
                    if (!item.IsAttended)
                    {
                        if (item.IsVacation)
                        {
                            worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                        }
                        else
                        {
                            worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[currentRow, 1, currentRow, headers.Length].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                        }
                    }
                }

                // Apply borders to data
                if (data.Count > 0)
                {
                    using (var dataRange = worksheet.Cells[startRow + 1, 1, startRow + data.Count, headers.Length])
                    {
                        dataRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }
                }

                worksheet.Cells.AutoFitColumns();

                return package.GetAsByteArray();
            }
        }

        public async Task<byte[]> ExportMonthlyAttendanceToExcelAsync(int year, int month)
        {
            var reportResult = await GetMonthlyAttendanceReportAsync(year, month);
            if (!reportResult.Success)
                throw new InvalidOperationException(reportResult.ErrorMessage);

            var data = reportResult.Data;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add($"Monthly Report {year}-{month:D2}");

                // Set the worksheet direction to Right-to-Left
                worksheet.View.RightToLeft = true;

                // Report title and date
                worksheet.Cells[1, 1].Value = "تقرير الحضور الشهري";
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1, 1, 11].Merge = true;

                worksheet.Cells[2, 1].Value = $"الشهر: {month:D2}/{year}";
                worksheet.Cells[2, 1].Style.Font.Bold = true;
                worksheet.Cells[2, 1, 2, 11].Merge = true;

                // Headers
                var startRow = 4;
                var headers = new string[]
                {
                    "اسم الموظف",
                    "القسم",
                    "ساعات العمل الكلية",
                    "ساعات التأخير الكلية",
                    "ساعات العمل الإضافي",
                    "أيام التأخير",
                    "أيام العمل الإضافي",
                    "ساعات العمل المتوقعة",
                    "أيام الحضور",
                    "أيام الغياب",
                    "أيام الإجازة",
                    "المرتب",
                    "المرتب الأساسي"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[startRow, i + 1].Value = headers[i];
                }

                // Style headers
                using (var headerRange = worksheet.Cells[startRow, 1, startRow, headers.Length])
                {
                    headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    headerRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    headerRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    headerRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                }

                // Data rows
                for (int row = 0; row < data.Count; row++)
                {
                    var item = data[row];
                    var currentRow = row + startRow + 1;

                    worksheet.Cells[currentRow, 1].Value = item.EmployeeName;
                    worksheet.Cells[currentRow, 2].Value = item.Department;
                    worksheet.Cells[currentRow, 3].Value = item.TotalMonthlyWorkingHours;
                    worksheet.Cells[currentRow, 4].Value = item.TotalDelayHours;
                    worksheet.Cells[currentRow, 5].Value = item.TotalOvertimeHours;
                    worksheet.Cells[currentRow, 6].Value = item.NumberOfDelayDays;
                    worksheet.Cells[currentRow, 7].Value = item.NumberOfOvertimeDays;
                    worksheet.Cells[currentRow, 8].Value = $"{item.ExpectedMonthlyWorkingHours:F2}";
                    worksheet.Cells[currentRow, 9].Value = item.PresentDays;
                    worksheet.Cells[currentRow, 10].Value = item.AbsentDays;
                    worksheet.Cells[currentRow, 11].Value = item.VacationDays;
                    worksheet.Cells[currentRow, 12].Value = item.Salary;
                    worksheet.Cells[currentRow, 13].Value = item.ExpectedSalary;

                    // Color coding for absent days
                    if (item.AbsentDays > 0)
                    {
                        worksheet.Cells[currentRow, 10].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        worksheet.Cells[currentRow, 10].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                    }
                }

                // Apply borders to data
                if (data.Count > 0)
                {
                    using (var dataRange = worksheet.Cells[startRow + 1, 1, startRow + data.Count, headers.Length])
                    {
                        dataRange.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        dataRange.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }
                }

                worksheet.Cells.AutoFitColumns();

                return package.GetAsByteArray();
            }
        }

        public async Task<byte[]> ExportDailyAttendanceToPdfAsync(DateOnly date)
        {
            var reportResult = await GetDailyAttendanceReportAsync(date);
            if (!reportResult.Success)
                throw new InvalidOperationException(reportResult.ErrorMessage);

            var data = reportResult.Data;

            // Register Arabic font (you need to have the font file)
            var fontPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            var arabicFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);

            using (var stream = new MemoryStream())
            {
                using (var writer = new PdfWriter(stream))
                using (var pdf = new PdfDocument(writer))
                using (var document = new Document(pdf, PageSize.A4.Rotate()))
                {
                    // Set document to RTL
                    document.SetProperty(Property.TEXT_ALIGNMENT, TextAlignment.RIGHT);

                    // Add Arabic title
                    var title = new Paragraph("تقرير الحضور اليومي")
                        .SetFont(arabicFont)
                        .SetFontSize(18)
                        .SetBold()
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(10);
                    document.Add(title);

                    // Add date in Arabic
                    var dateParagraph = new Paragraph($"تاريخ التقرير: {date:yyyy-MM-dd}")
                        .SetFont(arabicFont)
                        .SetFontSize(12)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginBottom(20);
                    document.Add(dateParagraph);

                    // Arabic headers
                    string[] headers = {
                "اسم الموظف", "القسم", "وقت الحضور", "وقت الانصراف",
                "مدة التأخير", "ساعات العمل الكلية", "حالة الحضور", "في إجازة"
            };

                    // Create table with appropriate column widths for Arabic text
                    float[] columnWidths = { 3f, 2f, 2f, 2f, 2f, 2f, 2f, 1.5f };
                    Table table = new Table(UnitValue.CreatePercentArray(columnWidths))
                        .UseAllAvailableWidth()
                        .SetTextAlignment(TextAlignment.RIGHT);

                    // Add Arabic headers
                    foreach (var header in headers)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new Paragraph(header).SetFont(arabicFont))
                            .SetBackgroundColor(new DeviceRgb(200, 200, 200))
                            .SetBold()
                            .SetPadding(8)
                            .SetTextAlignment(TextAlignment.CENTER));
                    }

                    // Add data rows with Arabic formatting
                    foreach (var item in data)
                    {
                        // Employee Name
                        table.AddCell(new Cell()
                            .Add(new Paragraph(item.EmployeeName ?? "").SetFont(arabicFont))
                            .SetPadding(6));

                        // Department
                        table.AddCell(new Cell()
                            .Add(new Paragraph(item.Department ?? "").SetFont(arabicFont))
                            .SetPadding(6));

                        // Attendance Time
                        table.AddCell(new Cell()
                            .Add(new Paragraph(item.AttendanceTime?.ToString("HH:mm") ?? ""))
                            .SetPadding(6)
                            .SetTextAlignment(TextAlignment.CENTER));

                        // Departure Time
                        table.AddCell(new Cell()
                            .Add(new Paragraph(item.DepartureTime?.ToString("HH:mm") ?? ""))
                            .SetPadding(6)
                            .SetTextAlignment(TextAlignment.CENTER));

                        // Delay Duration
                        table.AddCell(new Cell()
                            .Add(new Paragraph(item.DelayDurationHours.ToString()))
                            .SetPadding(6)
                            .SetTextAlignment(TextAlignment.CENTER));

                        // Total Working Hours
                        table.AddCell(new Cell()
                            .Add(new Paragraph(item.TotalWorkingHours.ToString()))
                            .SetPadding(6)
                            .SetTextAlignment(TextAlignment.CENTER));


                        // Attendance Status (Arabic)
                        var statusText = item.IsAttended ? "حاضر" : "غائب";
                        var statusCell = new Cell()
                            .Add(new Paragraph(statusText).SetFont(arabicFont))
                            .SetPadding(6)
                            .SetTextAlignment(TextAlignment.CENTER);

                        if (!item.IsAttended)
                        {
                            statusCell.SetBackgroundColor(item.IsVacation ?
                                new DeviceRgb(144, 238, 144) : // Light green for vacation
                                new DeviceRgb(255, 182, 193)); // Light red for absence
                        }
                        table.AddCell(statusCell);

                        // On Leave (Arabic)
                        var leaveText = item.IsVacation ? "نعم" : "لا";
                        table.AddCell(new Cell()
                            .Add(new Paragraph(leaveText).SetFont(arabicFont))
                            .SetPadding(6)
                            .SetTextAlignment(TextAlignment.CENTER));
                    }

                    document.Add(table);

                    // Add summary
                    if (data.Count > 0)
                    {
                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));

                        var summaryTitle = new Paragraph("ملخص التقرير")
                            .SetFont(arabicFont)
                            .SetFontSize(16)
                            .SetBold()
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetMarginBottom(15);
                        document.Add(summaryTitle);

                        var presentCount = data.Count(x => x.IsAttended);
                        var absentCount = data.Count(x => !x.IsAttended && !x.IsVacation);
                        var vacationCount = data.Count(x => x.IsVacation);

                        var summaryContent = new Paragraph()
                            .SetFont(arabicFont)
                            .SetFontSize(12)
                            .Add($"إجمالي الموظفين: {data.Count}\n")
                            .Add($"عدد الحاضرين: {presentCount}\n")
                            .Add($"عدد الغائبين: {absentCount}\n")
                            .Add($"عدد الموجودين في إجازة: {vacationCount}")
                            .SetTextAlignment(TextAlignment.RIGHT)
                            .SetMarginBottom(20);

                        document.Add(summaryContent);
                    }
                }

                return stream.ToArray();
            }
        }

        public async Task<byte[]> ExportMonthlyAttendanceToPdfAsync(int year, int month)
        {
            var reportResult = await GetMonthlyAttendanceReportAsync(year, month);
            if (!reportResult.Success)
                throw new InvalidOperationException(reportResult.ErrorMessage);

            var data = reportResult.Data;

            // Register Arabic font
            var fontPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            var arabicFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);

            using (var stream = new MemoryStream())
            {
                using (var writer = new PdfWriter(stream))
                using (var pdf = new PdfDocument(writer))
                using (var document = new Document(pdf, PageSize.A4.Rotate()))
                {
                    // Set document to RTL
                    document.SetProperty(Property.TEXT_ALIGNMENT, TextAlignment.RIGHT);

                    // Arabic title
                    var title = new Paragraph("تقرير الحضور الشهري")
                        .SetFont(arabicFont)
                        .SetFontSize(18)
                        .SetBold()
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMarginBottom(10);
                    document.Add(title);

                    // Arabic date
                    var dateParagraph = new Paragraph($"الشهر: {month:D2}/{year}")
                        .SetFont(arabicFont)
                        .SetFontSize(12)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginBottom(20);
                    document.Add(dateParagraph);

                    // Arabic headers
                    string[] headers = {
                "اسم الموظف", "القسم", "ساعات العمل الكلية", "ساعات التأخير الكلية",
                "ساعات العمل الإضافي", "أيام التأخير", "أيام العمل الإضافي",
                "ساعات العمل المتوقعة", "أيام الحضور", "أيام الغياب", "أيام الإجازة"
            };

                    float[] columnWidths = { 2.5f, 2f, 1.5f, 1.5f, 1.5f, 1f, 1f, 1.5f, 1f, 1f, 1f };
                    Table table = new Table(UnitValue.CreatePercentArray(columnWidths))
                        .UseAllAvailableWidth()
                        .SetTextAlignment(TextAlignment.RIGHT);

                    // Add Arabic headers
                    foreach (var header in headers)
                    {
                        table.AddHeaderCell(new Cell()
                            .Add(new Paragraph(header).SetFont(arabicFont))
                            .SetBackgroundColor(new DeviceRgb(200, 200, 200))
                            .SetBold()
                            .SetPadding(6)
                            .SetTextAlignment(TextAlignment.CENTER));
                    }

                    // Add data rows
                    foreach (var item in data)
                    {
                        table.AddCell(new Cell().Add(new Paragraph(item.EmployeeName ?? "").SetFont(arabicFont)).SetPadding(4));
                        table.AddCell(new Cell().Add(new Paragraph(item.Department ?? "").SetFont(arabicFont)).SetPadding(4));
                        table.AddCell(new Cell().Add(new Paragraph(item.TotalMonthlyWorkingHours.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));
                        table.AddCell(new Cell().Add(new Paragraph(item.TotalDelayHours.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));
                        table.AddCell(new Cell().Add(new Paragraph(item.TotalOvertimeHours.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));
                        table.AddCell(new Cell().Add(new Paragraph(item.NumberOfDelayDays.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));
                        table.AddCell(new Cell().Add(new Paragraph(item.NumberOfOvertimeDays.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));
                        table.AddCell(new Cell().Add(new Paragraph(item.ExpectedMonthlyWorkingHours.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));

                        // Present days - normal
                        table.AddCell(new Cell().Add(new Paragraph(item.PresentDays.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));

                        // Absent days - highlighted
                        var absentCell = new Cell().Add(new Paragraph(item.AbsentDays.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER);
                        if (item.AbsentDays > 0)
                        {
                            absentCell.SetBackgroundColor(new DeviceRgb(255, 182, 193)); // Light red
                        }
                        table.AddCell(absentCell);

                        table.AddCell(new Cell().Add(new Paragraph(item.VacationDays.ToString())).SetPadding(4).SetTextAlignment(TextAlignment.CENTER));
                    }

                    document.Add(table);
                }

                return stream.ToArray();
            }
        }
        private int CalculateWorkingDaysInMonth(int year, int month, List<int> weekendDays)
        {
            var firstDayOfMonth = new DateTime(year, month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            int workingDays = 0;

            for (DateTime date = firstDayOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
            {
                if (!weekendDays.Contains((int)date.DayOfWeek))
                {
                    workingDays++;
                }
            }

            return workingDays;
        }

    }
}
