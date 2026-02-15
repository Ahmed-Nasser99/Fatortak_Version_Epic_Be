using fatortak.Common.Enum;
using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.User
{
    public class UserCreateDto
    {
        [Required]
        public string Email { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string Password { get; set; }

        public string? Role { get; set; } = RoleEnum.Watcher.ToString(); // "Admin", "Manager", "User", etc.

        public string PhoneNumber { get; set; } // Optional, can be used for notifications or two-factor authentication
    }
}
