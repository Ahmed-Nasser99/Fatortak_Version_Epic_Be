namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for JournalEntryLine entity
    /// </summary>
    public class JournalEntryLineDto
    {
        public Guid Id { get; set; }
        public Guid JournalEntryId { get; set; }
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
    }
}

