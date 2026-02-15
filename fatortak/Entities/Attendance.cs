namespace fatortak.Entities
{
    public class Attendance : baseEntitiy, ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid EmployeeId { get; set; }

        public DateTime Date { get; set; }
        public DateTime? AttendTime { get; set; }
        public DateTime? LeaveTime { get; set; }

        public string? Status { get; set; }
        public string? Reason { get; set; }
        public Tenant Tenant { get; set; }
        public Employee Employee { get; set; } = null!;
    }
}
