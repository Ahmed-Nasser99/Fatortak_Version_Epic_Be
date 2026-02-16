using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using fatortak.Common.Enum;

namespace fatortak.Entities
{
    public class Transaction
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid TenantId { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; }

        [Required]
        public string Type { get; set; } // InvoiceIssued, PaymentReceived, Expense, etc.

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Direction { get; set; } // Credit, Debit

        public string? ReferenceId { get; set; } // InvoiceId, ExpenseId

        public string? ReferenceType { get; set; } // "Invoice", "Expense"

        public string? Description { get; set; }

        public string? PaymentMethod { get; set; } // Cash, BankTransfer, etc.

        public Guid? CreatedBy { get; set; }

        public Guid? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public Guid? ProjectId { get; set; }
        public Project? Project { get; set; }


        public string? AttachmentUrl { get; set; }
        
        public string? Category { get; set; } // Expense Category or Revenue Type

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
