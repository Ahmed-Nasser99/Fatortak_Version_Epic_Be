using fatortak.Dtos.Email;
using fatortak.Entities;

namespace fatortak.Services.EmailService
{
    public interface IEmailService
    {
        Task<EmailResponseViewModel> ForgotPasswordAsync(ApplicationUser user);
    }
}
