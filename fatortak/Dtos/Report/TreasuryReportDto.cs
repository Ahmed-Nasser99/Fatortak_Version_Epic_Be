using fatortak.Dtos.Transaction;

namespace fatortak.Dtos.Report
{
    public class TreasuryReportDto
    {
        public decimal TotalBalance { get; set; }
        public List<AccountBalanceDto> Accounts { get; set; } = new();
        public List<TransactionDto> Transactions { get; set; } = new();
    }

    public class AccountBalanceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; }
    }
}
