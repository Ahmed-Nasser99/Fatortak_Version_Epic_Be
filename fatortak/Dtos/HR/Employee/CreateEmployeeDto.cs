namespace fatortak.Dtos.HR.Employee
{
    public class CreateEmployeeDto
    {
        public Guid DepartmentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; } = string.Empty;
        public string? Phone { get; set; } = string.Empty;
        public string? Position { get; set; } = string.Empty;
        public DateTime? HireDate { get; set; }
        public decimal? Salary { get; set; }
    }
}
