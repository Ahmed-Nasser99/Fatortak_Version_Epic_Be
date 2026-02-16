using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    /// <summary>
    /// Represents an account in the Chart of Accounts.
    /// Supports hierarchical structure with parent-child relationships.
    /// Only leaf accounts (accounts without children) can have journal entries posted to them.
    /// </summary>
    public class Account : ITenantEntity
    {
        /// <summary>
        /// Unique identifier for the account
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Tenant identifier for multi-tenant support
        /// </summary>
        [Required]
        public Guid TenantId { get; set; }

        /// <summary>
        /// Unique account code within the tenant (e.g., "1000", "1100", "4000")
        /// Must be unique per tenant
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string AccountCode { get; set; }

        /// <summary>
        /// Display name of the account (e.g., "Cash", "Accounts Receivable", "Sales Revenue")
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        /// <summary>
        /// Type of account (Asset, Liability, Equity, Revenue, Expense)
        /// Determines balance calculation rules
        /// </summary>
        [Required]
        public AccountType AccountType { get; set; }

        /// <summary>
        /// Reference to parent account for hierarchical structure
        /// Null for root-level accounts
        /// </summary>
        public Guid? ParentAccountId { get; set; }

        /// <summary>
        /// Navigation property to parent account
        /// </summary>
        [ForeignKey(nameof(ParentAccountId))]
        public Account? ParentAccount { get; set; }

        /// <summary>
        /// Navigation property to child accounts
        /// </summary>
        public ICollection<Account> ChildAccounts { get; set; } = new List<Account>();

        /// <summary>
        /// Hierarchical level in the chart (0 = root, 1 = first level, etc.)
        /// Used for reporting and validation
        /// </summary>
        [Required]
        public int Level { get; set; }

        /// <summary>
        /// Indicates if the account is active and can be used
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Indicates if journal entries can be posted to this account
        /// Only leaf accounts (accounts without children) should be postable
        /// </summary>
        [Required]
        public bool IsPostable { get; set; }

        /// <summary>
        /// Optional description or notes about the account
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Timestamp when the account was created
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the account was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// User who created the account
        /// </summary>
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// User who last updated the account
        /// </summary>
        public Guid? UpdatedBy { get; set; }

        /// <summary>
        /// Navigation property to tenant
        /// </summary>
        public Tenant Tenant { get; set; }

        /// <summary>
        /// Navigation property to journal entry lines
        /// </summary>
        public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

        /// <summary>
        /// Validates that the account can have journal entries posted to it
        /// </summary>
        public bool CanPost()
        {
            return IsActive && IsPostable && ChildAccounts.Count == 0;
        }
    }
}

