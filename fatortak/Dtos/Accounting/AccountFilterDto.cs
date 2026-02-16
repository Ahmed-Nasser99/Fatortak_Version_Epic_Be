using fatortak.Common.Enum;

namespace fatortak.Dtos.Accounting
{
    /// <summary>
    /// DTO for filtering accounts
    /// </summary>
    public class AccountFilterDto
    {
        public string? AccountCode { get; set; }
        public string? Name { get; set; }
        public AccountType? AccountType { get; set; }
        public Guid? ParentAccountId { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsPostable { get; set; }
        public bool? IncludeInactive { get; set; }
    }
}

