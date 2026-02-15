using fatortak.Common.Enum;

namespace fatortak.Dtos.Auth
{
    public class RegisterDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Subdomain { get; set; }
        public string? PhoneNumber { get; set; }
        public string CompanyName { get; set; }
        public string? Address { get; set; }
        public string Currency { get; set; }
        public string? Role { get; set; } = RoleEnum.Admin.ToString();
    }
}
