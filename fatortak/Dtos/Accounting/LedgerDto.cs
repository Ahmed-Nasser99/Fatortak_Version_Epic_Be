namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for account ledger (running balance)
    /// </summary>
    public class LedgerDto
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<LedgerEntryDto> Entries { get; set; } = new List<LedgerEntryDto>();
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
    }

    /// <summary>
    /// DTO for a single ledger entry
    /// </summary>
    public class LedgerEntryDto
    {
        public Guid JournalEntryId { get; set; }
        public string EntryNumber { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
        public string? Reference { get; set; }
    }
}

