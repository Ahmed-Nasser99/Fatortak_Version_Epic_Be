namespace fatortak.Dtos.HR.Employee
{
    public class EmployeeDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public Guid DepartmentId { get; set; }

        public DateTime? HireDate { get; set; }
        public decimal? Salary { get; set; }
    }
}
