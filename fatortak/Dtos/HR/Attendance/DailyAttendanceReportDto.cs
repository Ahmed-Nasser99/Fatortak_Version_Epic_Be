namespace fatortak.Dtos.HR.Attendance
{
    public class DailyAttendanceReportDto
    {
        public DateOnly Date { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }
        public TimeSpan? AttendanceTime { get; set; }
        public TimeSpan? DepartureTime { get; set; }
        public TimeSpan? DelayDurationHours { get; set; }
        public TimeSpan? TotalWorkingHours { get; set; }
        public bool IsAttended { get; set; } = true;
        public bool IsVacation { get; set; } = false;
    }
}
