namespace fatortak.Dtos.HR.Attendance
{
    public class MonthlyAttendanceReportDto
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Department { get; set; }

        public string TotalMonthlyWorkingHours { get; set; }
        public string TotalDelayHours { get; set; }
        public string TotalOvertimeHours { get; set; }

        public int NumberOfDelayDays { get; set; }
        public int NumberOfOvertimeDays { get; set; }

        public string ExpectedMonthlyWorkingHours { get; set; }
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int VacationDays { get; set; }
        public decimal? Salary { get; set; }
        public decimal? ExpectedSalary { get; set; }
    }

}
