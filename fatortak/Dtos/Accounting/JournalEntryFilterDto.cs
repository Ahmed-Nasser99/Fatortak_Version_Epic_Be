using fatortak.Common.Enum;

namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for filtering journal entries
    /// </summary>
    public class JournalEntryFilterDto
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public JournalEntryReferenceType? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }
        public bool? IsPosted { get; set; }
        public Guid? AccountId { get; set; }
        public string? EntryNumber { get; set; }
        public Guid? ProjectId { get; set; }
    }
}

