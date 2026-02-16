namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for account balance information
    /// </summary>
    public class AccountBalanceDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal DebitTotal { get; set; }
        public decimal CreditTotal { get; set; }
        public decimal Balance { get; set; } // Calculated based on account type
        public DateTime? AsOfDate { get; set; }
    }
}

