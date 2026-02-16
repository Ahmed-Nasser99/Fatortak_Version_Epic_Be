using System;
using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Transaction
{
    public class TransferDto
    {
        [Required]
        public Guid FromAccountId { get; set; }

        [Required]
        public Guid ToAccountId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public string? Description { get; set; }

        public Guid? BranchId { get; set; }

        public Guid? ProjectId { get; set; }
    }
}
