using fatortak.Common.Enum;

namespace fatortak.Dtos
{
    public class FinancialAccountDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public FinancialAccountType Type { get; set; }
        public string? AccountNumber { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateFinancialAccountDto
    {
        public string Name { get; set; }
        public FinancialAccountType Type { get; set; }
        public string? AccountNumber { get; set; }
        public Guid? EmployeeId { get; set; }
        public decimal InitialBalance { get; set; } = 0;
        public string Currency { get; set; } = "EGP";
    }

    public class UpdateFinancialAccountDto
    {
        public string Name { get; set; }
        public string? AccountNumber { get; set; }
        // Type usually shouldn't change easily, but name/number can
        // Balance is updated via transactions only
    }
}
