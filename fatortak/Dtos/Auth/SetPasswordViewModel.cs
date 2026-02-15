using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Auth
{
    public class SetPasswordViewModel
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        public string NewPassword { get; set; }
    }
}
