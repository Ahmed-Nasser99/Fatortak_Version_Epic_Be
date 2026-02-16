using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for creating a manual journal entry
    /// </summary>
    public class JournalEntryCreateDto
    {
        [Required]
        public DateTime Date { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MinLength(2, ErrorMessage = "A journal entry must have at least 2 lines")]
        public List<JournalEntryLineCreateDto> Lines { get; set; } = new List<JournalEntryLineCreateDto>();
    }

    /// <summary>
    /// DTO for creating a journal entry line
    /// </summary>
    public class JournalEntryLineCreateDto
    {
        [Required]
        public Guid AccountId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Debit must be >= 0")]
        public decimal Debit { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "Credit must be >= 0")]
        public decimal Credit { get; set; } = 0;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }
    }
}

