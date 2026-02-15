using fatortak.Dtos.Auth;
using fatortak.Dtos.Shared;

namespace fatortak.Services.AuthService
{
    public interface IAuthService
    {
        Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterDto dto);
        Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto dto);

        Task<ServiceResult<string>> ForgetPasswordRequestAsync(string email);
        Task<ServiceResult<string>> SetNewPassword(string userId ,string token, string NewPassword);
    }
}
