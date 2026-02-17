using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    /// <summary>
    /// Represents a journal entry in the double-entry accounting system.
    /// Each journal entry must have balanced debits and credits.
    /// </summary>
    public class JournalEntry : ITenantEntity
    {
        /// <summary>
        /// Unique identifier for the journal entry
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Tenant identifier for multi-tenant support
        /// </summary>
        [Required]
        public Guid TenantId { get; set; }

        /// <summary>
        /// Auto-incremental entry number per tenant (e.g., JE-0001, JE-0002)
        /// Used for human-readable reference
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EntryNumber { get; set; }

        /// <summary>
        /// Date of the journal entry
        /// </summary>
        [Required]
        [Column(TypeName = "date")]
        public DateTime Date { get; set; }

        /// <summary>
        /// Type of source document that generated this entry
        /// </summary>
        [Required]
        public JournalEntryReferenceType ReferenceType { get; set; }

        /// <summary>
        /// ID of the source document (InvoiceId, ExpenseId, etc.)
        /// Null for manual entries
        /// </summary>
        public Guid? ReferenceId { get; set; }

        /// <summary>
        /// Description or memo for the journal entry
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Indicates if the entry has been posted (finalized)
        /// Once posted, entries should not be modified
        /// </summary>
        [Required]
        public bool IsPosted { get; set; } = false;

        /// <summary>
        /// Timestamp when the entry was posted
        /// </summary>
        public DateTime? PostedAt { get; set; }

        /// <summary>
        /// User who created the entry
        /// </summary>
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// User who posted the entry
        /// </summary>
        public Guid? PostedBy { get; set; }

        /// <summary>
        /// Timestamp when the entry was created
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the entry was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Reference to reversing entry (if this entry reverses another)
        /// </summary>
        public Guid? ReversingEntryId { get; set; }

        /// <summary>
        /// Navigation property to reversing entry
        /// </summary>
        [ForeignKey(nameof(ReversingEntryId))]
        public JournalEntry? ReversingEntry { get; set; }

        /// <summary>
        /// Optional project linked to this journal entry
        /// </summary>
        public Guid? ProjectId { get; set; }

        /// <summary>
        /// Navigation property to project
        /// </summary>
        [ForeignKey(nameof(ProjectId))]
        public Project? Project { get; set; }

        /// <summary>
        /// Navigation property to tenant
        /// </summary>
        public Tenant Tenant { get; set; }

        /// <summary>
        /// Navigation property to journal entry lines
        /// </summary>
        public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();

        /// <summary>
        /// Calculates total debit amount for all lines
        /// </summary>
        [NotMapped]
        public decimal TotalDebit => Lines?.Sum(l => l.Debit) ?? 0;

        /// <summary>
        /// Calculates total credit amount for all lines
        /// </summary>
        [NotMapped]
        public decimal TotalCredit => Lines?.Sum(l => l.Credit) ?? 0;

        /// <summary>
        /// Validates that debits equal credits
        /// </summary>
        public bool IsBalanced()
        {
            return Math.Abs(TotalDebit - TotalCredit) < 0.01m; // Allow for rounding differences
        }
    }
}

