using fatortak.Dtos.Shared;
using fatortak.Dtos.UserProfile;
using fatortak.Helpers;
using fatortak.Services.UserService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserProfileService _userProfileService;
        private readonly ILogger<UserProfileController> _logger;

        public UserProfileController(
            IUserProfileService userProfileService,
            ILogger<UserProfileController> logger)
        {
            _userProfileService = userProfileService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<UserProfileDto>>> GetCurrentUserProfile()
        {
            try
            {
                var result = await _userProfileService.GetCurrentUserProfileAsync();

                if (!result.Success)
                {
                    if (result.ErrorMessage == "User not found")
                        return NotFound(result);

                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user profile");
                return BadRequest(ServiceResult<UserProfileDto>.Failure("An error occurred while retrieving profile"));
            }
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<ServiceResult<UserProfileDto>>> GetUserProfile(Guid userId)
        {
            try
            {
                var result = await _userProfileService.GetUserProfileAsync(userId);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "User not found")
                        return NotFound(result);

                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user profile {userId}");
                return BadRequest(ServiceResult<UserProfileDto>.Failure("An error occurred while retrieving profile"));
            }
        }

        [HttpPost("update")]
        public async Task<ActionResult<ServiceResult<UserProfileDto>>> UpdateProfile([FromBody] UserProfileUpdateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<UserProfileDto>.ValidationError(errors));
                }

                var result = await _userProfileService.UpdateUserProfileAsync(dto);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "User not found")
                        return NotFound(result);

                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile");
                return BadRequest(ServiceResult<UserProfileDto>.Failure("An error occurred while updating profile"));
            }
        }

        [HttpPost("profile-picture")]
        public async Task<ActionResult<ServiceResult<string>>> UploadProfilePicture([FromForm] UpdateUserProfileImageDto dto)
        {
            try
            {
                if (dto.file == null || dto.file.Length == 0)
                    return BadRequest(ServiceResult<string>.Failure("No file uploaded"));

                var result = await _userProfileService.UpdateProfilePictureAsync(dto);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture");
                return BadRequest(ServiceResult<string>.Failure("An error occurred while uploading profile picture"));
            }
        }

        [HttpPost("delete-profile-picture")]
        public async Task<ActionResult<ServiceResult<bool>>> RemoveProfilePicture()
        {
            try
            {
                var result = await _userProfileService.RemoveProfilePictureAsync();

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing profile picture");
                return BadRequest(ServiceResult<bool>.Failure("An error occurred while removing profile picture"));
            }
        }

        [HttpPost("change-password")]
        public async Task<ActionResult<ServiceResult<bool>>> ChangePassword([FromBody] fatortak.Dtos.UserProfile.ChangePasswordDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<bool>.ValidationError(errors));
                }

                var result = await _userProfileService.ChangePasswordAsync(dto);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return BadRequest(ServiceResult<bool>.Failure("An error occurred while changing password"));
            }
        }

        [HttpPost("{userId}/status")]
        public async Task<ActionResult<ServiceResult<bool>>> UpdateUserStatus(
            Guid userId,
            [FromBody] UpdateUserStatusDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<bool>.ValidationError(errors));
                }

                var UserIdFromHelper = UserHelper.GetUserId();
                if (string.IsNullOrEmpty(UserIdFromHelper))
                {
                    return Unauthorized(ServiceResult<bool>.Failure("You cannot change your own status"));
                }
                var currentUserId = new Guid(UserIdFromHelper);

                if (userId == currentUserId)
                    return BadRequest(ServiceResult<bool>.Failure("You cannot change your own status"));

                var result = await _userProfileService.UpdateUserStatusAsync(userId, dto.IsActive);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "User not found")
                        return NotFound(result);

                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for user {userId}");
                return BadRequest(ServiceResult<bool>.Failure("An error occurred while updating user status"));
            }
        }
    }
}