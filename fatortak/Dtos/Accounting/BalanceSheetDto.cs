namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for Balance Sheet report
    /// </summary>
    public class BalanceSheetDto
    {
        public DateTime AsOfDate { get; set; }
        public List<BalanceSheetItemDto> Assets { get; set; } = new List<BalanceSheetItemDto>();
        public List<BalanceSheetItemDto> Liabilities { get; set; } = new List<BalanceSheetItemDto>();
        public List<BalanceSheetItemDto> Equity { get; set; } = new List<BalanceSheetItemDto>();
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public bool IsBalanced { get; set; } // Assets should equal Liabilities + Equity
    }

    /// <summary>
    /// DTO for a single balance sheet item
    /// </summary>
    public class BalanceSheetItemDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal Balance { get; set; }
    }
}

