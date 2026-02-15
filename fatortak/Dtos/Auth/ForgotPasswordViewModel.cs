using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Auth
{
    public class ForgotPasswordViewModel
    {
        [Required]
        public string Email { get; set; }
    }
}
