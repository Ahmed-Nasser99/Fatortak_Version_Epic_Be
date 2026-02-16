using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    /// <summary>
    /// Represents a single line in a journal entry.
    /// Each line must have either a debit or credit amount (not both, not neither).
    /// </summary>
    public class JournalEntryLine
    {
        /// <summary>
        /// Unique identifier for the journal entry line
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Foreign key to the parent journal entry
        /// </summary>
        [Required]
        public Guid JournalEntryId { get; set; }

        /// <summary>
        /// Navigation property to parent journal entry
        /// </summary>
        [ForeignKey(nameof(JournalEntryId))]
        public JournalEntry JournalEntry { get; set; }

        /// <summary>
        /// Foreign key to the account being debited or credited
        /// </summary>
        [Required]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Navigation property to account
        /// </summary>
        [ForeignKey(nameof(AccountId))]
        public Account Account { get; set; }

        /// <summary>
        /// Debit amount (must be >= 0)
        /// One of Debit or Credit must be zero
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Debit { get; set; } = 0;

        /// <summary>
        /// Credit amount (must be >= 0)
        /// One of Debit or Credit must be zero
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Credit { get; set; } = 0;

        /// <summary>
        /// Optional description for this specific line
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Optional reference number or identifier
        /// </summary>
        [MaxLength(100)]
        public string? Reference { get; set; }

        /// <summary>
        /// Validates that the line has exactly one non-zero amount
        /// </summary>
        public bool IsValid()
        {
            return Debit >= 0 && Credit >= 0 &&
                   (Debit == 0 || Credit == 0) &&
                   (Debit != 0 || Credit != 0);
        }
    }
}

