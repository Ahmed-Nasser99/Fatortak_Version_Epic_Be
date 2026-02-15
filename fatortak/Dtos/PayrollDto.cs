using fatortak.Entities;

namespace fatortak.Dtos
{
    public class PayrollDto
    {
        public Guid Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public int? ExpenseId { get; set; }
        public Guid? TransactionId { get; set; }
        public List<PayrollItemDto> PayrollItems { get; set; } = new List<PayrollItemDto>();
    }

    public class PayrollItemDto
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal CalculatedSalary { get; set; }
        public int DaysAttended { get; set; }
        public string CalculationMethod { get; set; }
    }

    public class GeneratePayrollDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string CalculationMethod { get; set; } // "MainSalary" or "AttendanceBased"
        public bool IsGlobal { get; set; } // true = all employees, false = specific (not implemented in this step but good for future)
        public List<Guid>? SpecificEmployeeIds { get; set; }
    }

    public class SubmitPayrollDto
    {
        public Guid PayrollId { get; set; }
    }
}
