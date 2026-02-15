using fatortak.Common.Enum;

namespace fatortak.Dtos
{
    public class FinancialAccountDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public FinancialAccountType Type { get; set; }
        public string? AccountNumber { get; set; }
        public string? BankName { get; set; }
        public string? Iban { get; set; }
        public string? Swift { get; set; }
        public string? Description { get; set; }
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
        public string? BankName { get; set; }
        public string? Iban { get; set; }
        public string? Swift { get; set; }
        public string? Description { get; set; }
        public Guid? EmployeeId { get; set; }
        public decimal InitialBalance { get; set; } = 0;
        public string Currency { get; set; } = "EGP";
    }

    public class UpdateFinancialAccountDto
    {
        public string Name { get; set; }
        public FinancialAccountType Type { get; set; }
        public string? AccountNumber { get; set; }
        public string? BankName { get; set; }
        public string? Iban { get; set; }
        public string? Swift { get; set; }
        public string? Description { get; set; }
    }
}
