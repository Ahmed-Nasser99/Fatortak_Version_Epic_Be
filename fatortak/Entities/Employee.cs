namespace fatortak.Entities
{
    public class Employee : baseEntitiy, ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }
        public Guid DepartmentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; } = string.Empty;
        public string? Phone { get; set; } = string.Empty;
        public string? Position { get; set; } = string.Empty;
        public DateTime? HireDate { get; set; }
        public decimal? Salary { get; set; }
        public Tenant Tenant { get; set; }
        public Department Department { get; set; } = null!;
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }

}
