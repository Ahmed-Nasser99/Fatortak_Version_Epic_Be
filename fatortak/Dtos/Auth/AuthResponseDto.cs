using fatortak.Dtos.Company;

namespace fatortak.Dtos.Auth
{
    public class AuthResponseDto
    {
        public string Token { get; set; }
        public UserDto User { get; set; }
        public TenantDto Tenant { get; set; }
        public CompanyDto Company { get; set; }
        public IEnumerable<string> Roles { get; set; }
    }
}
