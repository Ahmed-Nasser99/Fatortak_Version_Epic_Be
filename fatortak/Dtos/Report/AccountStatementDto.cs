namespace fatortak.Dtos.Report
{
    public class AccountStatementDto
    {
        public CustomerStatementInfoDto CustomerInfo { get; set; }
        public List<AccountStatementTransactionDto> Transactions { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
    }
}
