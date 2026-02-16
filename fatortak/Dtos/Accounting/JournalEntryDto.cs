using fatortak.Common.Enum;

namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for JournalEntry entity
    /// </summary>
    public class JournalEntryDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string EntryNumber { get; set; }
        public DateTime Date { get; set; }
        public JournalEntryReferenceType ReferenceType { get; set; }
        public string ReferenceTypeName { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? Description { get; set; }
        public bool IsPosted { get; set; }
        public DateTime? PostedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? PostedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? ReversingEntryId { get; set; }
        public List<JournalEntryLineDto> Lines { get; set; } = new List<JournalEntryLineDto>();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }
}

