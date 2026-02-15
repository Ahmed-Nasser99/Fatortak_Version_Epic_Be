using fatortak.Common.Enum;
using Microsoft.AspNetCore.Identity;

namespace fatortak.Entities
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? Role { get; set; } = RoleEnum.Watcher.ToString();
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}