using fatortak.Common.Enum;

namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for Account entity
    /// </summary>
    public class AccountDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string AccountCode { get; set; }
        public string Name { get; set; }
        public AccountType AccountType { get; set; }
        public string AccountTypeName { get; set; }
        public Guid? ParentAccountId { get; set; }
        public string? ParentAccountName { get; set; }
        public int Level { get; set; }
        public bool IsActive { get; set; }
        public bool IsPostable { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<AccountDto> ChildAccounts { get; set; } = new List<AccountDto>();
        public decimal? Balance { get; set; } // Calculated balance
    }
}

