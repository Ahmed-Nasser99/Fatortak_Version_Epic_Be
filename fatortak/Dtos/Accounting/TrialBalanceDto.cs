namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for trial balance report
    /// </summary>
    public class TrialBalanceDto
    {
        public List<TrialBalanceItemDto> Items { get; set; } = new List<TrialBalanceItemDto>();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public bool IsBalanced { get; set; }
        public DateTime? AsOfDate { get; set; }
    }

    /// <summary>
    /// DTO for a single trial balance item
    /// </summary>
    public class TrialBalanceItemDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal DebitTotal { get; set; }
        public decimal CreditTotal { get; set; }
        public decimal Balance { get; set; }
    }
}

