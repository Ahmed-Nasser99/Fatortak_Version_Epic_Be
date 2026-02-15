namespace fatortak.Dtos.HR.Attendance
{
    public class AttendanceDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime AttendanceDate { get; set; }
        public DateTime? AttendTime { get; set; }
        public DateTime? LeaveTime { get; set; }
        public string Status { get; set; }
        public string? Reason { get; set; }
    }
}
