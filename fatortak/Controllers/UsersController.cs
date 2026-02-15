using fatortak.Dtos.Shared;
using fatortak.Dtos.User;
using fatortak.Helpers;
using fatortak.Services.UserService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserService userService,
            ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ServiceResult<PagedResponseDto<UserDto>>>> GetUsers(
            [FromQuery] UserFilterDto filter,
            [FromQuery] PaginationDto pagination)
        {
            try
            {
                var currentUserId = UserHelper.GetUserId();

                if (currentUserId == null)
                {
                    return Unauthorized();
                }

                var currentUserIdGuid = new Guid(currentUserId);
                var result = await _userService.GetUsersAsync(filter, pagination);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return BadRequest(ServiceResult<PagedResponseDto<UserDto>>.Failure("An error occurred while retrieving users"));
            }
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<ServiceResult<UserDto>>> GetUser(Guid userId)
        {
            try
            {
                var currentUserId = UserHelper.GetUserId();

                if (currentUserId == null)
                {
                    return Unauthorized();
                }

                var currentUserIdGuid = new Guid(currentUserId);
                var result = await _userService.GetUserAsync(userId);

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
                _logger.LogError(ex, $"Error retrieving user {userId}");
                return BadRequest(ServiceResult<UserDto>.Failure("An error occurred while retrieving user"));
            }
        }

        [HttpPost]
        public async Task<ActionResult<ServiceResult<UserDto>>> CreateUser([FromBody] UserCreateDto dto)
        {
            try
            {

                var currentUserId = UserHelper.GetUserId();

                if (currentUserId == null)
                {
                    return Unauthorized();
                }

                var currentUserIdGuid = new Guid(currentUserId);

                var result = await _userService.CreateUserAsync(dto, currentUserIdGuid);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return CreatedAtAction(
                    nameof(GetUser),
                    new { userId = result.Data.Id },
                    result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return BadRequest(ServiceResult<UserDto>.Failure("An error occurred while creating user"));
            }
        }

        [HttpPost("update/{userId}")]
        public async Task<ActionResult<ServiceResult<UserDto>>> UpdateUser(
            Guid userId,
            [FromBody] UserUpdateDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ServiceResult<UserDto>.ValidationError(errors));
                }

                var currentUserId = UserHelper.GetUserId();

                if (currentUserId == null)
                {
                    return Unauthorized();
                }

                var currentUserIdGuid = new Guid(currentUserId);
                var result = await _userService.UpdateUserAsync(userId, dto, currentUserIdGuid);

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
                _logger.LogError(ex, $"Error updating user {userId}");
                return BadRequest(ServiceResult<UserDto>.Failure("An error occurred while updating user"));
            }
        }

        [HttpPost("delete/{userId}")]
        public async Task<ActionResult<ServiceResult<bool>>> DeleteUser(Guid userId)
        {
            try
            {
                var currentUserId = UserHelper.GetUserId();

                if (currentUserId == null)
                {
                    return Unauthorized();
                }

                var currentUserIdGuid = new Guid(currentUserId);
                var result = await _userService.DeleteUserAsync(userId, currentUserIdGuid);

                if (!result.Success)
                {
                    if (result.ErrorMessage == "User not found")
                        return NotFound(result);

                    return StatusCode(500, result);
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating user {userId}");
                return BadRequest(ServiceResult<bool>.Failure("An error occurred while deactivating user"));
            }
        }

        [HttpPost("{userId}/change-password")]
        public async Task<ActionResult<ServiceResult<bool>>> ChangePassword(
            Guid userId,
            [FromBody] ChangePasswordDto dto)
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

                // Verify user is changing their own password or is admin
                var currentUserId = UserHelper.GetUserId();

                if (currentUserId == null)
                {
                    return Unauthorized();
                }

                var currentUserIdGuid = new Guid(currentUserId);

                if (userId != currentUserIdGuid && !User.IsInRole("Admin"))
                    return Unauthorized();

                var result = await _userService.ChangePasswordAsync(userId, dto);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user {userId}");
                return BadRequest(ServiceResult<bool>.Failure("An error occurred while changing password"));
            }
        }

    }
}
