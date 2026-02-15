using fatortak.Dtos.Transaction;

namespace fatortak.Dtos.Report
{
    public class EmployeeCustodyReportDto
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal TotalSpent { get; set; }
        public List<TransactionDto> Transactions { get; set; } = new();
    }
}
