namespace fatortak.Dtos.HR.Attendance
{
    public class UpdateAttendanceDto
    {
        public DateTime? AttendanceDate { get; set; }
        public DateTime? AttendTime { get; set; }
        public DateTime? LeaveTime { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
    }
}
