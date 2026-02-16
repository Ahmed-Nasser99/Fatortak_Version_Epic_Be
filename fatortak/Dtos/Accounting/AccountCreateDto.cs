using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for creating a new account
    /// </summary>
    public class AccountCreateDto
    {
        [Required]
        [MaxLength(50)]
        public string AccountCode { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required]
        public AccountType AccountType { get; set; }

        public Guid? ParentAccountId { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsPostable { get; set; } = false;
    }
}

