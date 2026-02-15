using fatortak.Dtos.Auth;
using fatortak.Dtos.Shared;
using fatortak.Services.AuthService;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<ActionResult<ServiceResult<AuthResponseDto>>> Register(RegisterDto registerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<AuthResponseDto>.ValidationError(errors));
                }

                var result = await _authService.RegisterAsync(registerDto);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthController.Register");
                return StatusCode(500, ServiceResult<AuthResponseDto>.Failure("An unexpected error occurred during registration"));
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<ServiceResult<AuthResponseDto>>> Login(LoginDto loginDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<AuthResponseDto>.ValidationError(errors));
                }

                var result = await _authService.LoginAsync(loginDto);

                if (!result.Success)
                {
                    return Unauthorized(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthController.Login");
                return StatusCode(500, ServiceResult<AuthResponseDto>.Failure("An unexpected error occurred during login"));
            }
        }

        [HttpPost("ForgetPasswordRequest")]
        public async Task<ActionResult<ServiceResult<string>>> ForgetPasswordRequest([FromBody] ForgotPasswordViewModel model)
        {
            try
            {
                var result = await _authService.ForgetPasswordRequestAsync(model.Email);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthController.ForgetPasswordRequest");
                return StatusCode(500, ServiceResult<string>.Failure("An unexpected error occurred during password reset request"));
            }
        }

        [HttpPost("SetNewPassword")]
        public async Task<ActionResult<ServiceResult<string>>> SetNewPassword([FromBody] SetPasswordViewModel model)
        {
            try
            {
                var result = await _authService.SetNewPassword( model.UserId,model.Token, model.NewPassword);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuthController.SetNewPassword");
                return StatusCode(500, ServiceResult<string>.Failure("An unexpected error occurred during password reset"));
            }
        }

    }
}