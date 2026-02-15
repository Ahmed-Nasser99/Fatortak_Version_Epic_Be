using fatortak.Context;
using fatortak.Dtos.Shared;
using fatortak.Dtos.UserProfile;
using fatortak.Entities;
using fatortak.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.UserService
{

    public class UserProfileService : IUserProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserProfileService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserProfileService(
            ApplicationDbContext context,
            ILogger<UserProfileService> logger,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _environment = environment;
            _httpContextAccessor = httpContextAccessor;

            UserHelper.Configure(_httpContextAccessor);
        }

        public async Task<ServiceResult<UserProfileDto>> GetCurrentUserProfileAsync()
        {
            var userIdString = UserHelper.GetUserId();
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return ServiceResult<UserProfileDto>.Failure("User not authenticated");
            }

            return await GetUserProfileAsync(userId);
        }

        public async Task<ServiceResult<UserProfileDto>> GetUserProfileAsync(Guid userId)
        {
            try
            {
                var user = await _userManager.Users
                    .Include(u => u.Tenant)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return ServiceResult<UserProfileDto>.Failure("User not found");

                return ServiceResult<UserProfileDto>.SuccessResult(MapToDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user profile with ID: {userId}");
                return ServiceResult<UserProfileDto>.Failure("Failed to retrieve user profile");
            }
        }

        public async Task<ServiceResult<UserProfileDto>> UpdateUserProfileAsync(UserProfileUpdateDto dto)
        {
            var userIdString = UserHelper.GetUserId();
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return ServiceResult<UserProfileDto>.Failure("User not authenticated");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ServiceResult<UserProfileDto>.Failure("User not found");

                // Update basic profile information
                user.FirstName = dto.FirstName;
                user.LastName = dto.LastName;
                user.PhoneNumber = dto.PhoneNumber;

                // Email update requires special handling
                if (!string.IsNullOrEmpty(dto.Email) && user.Email != dto.Email)
                {
                    var emailExists = await _userManager.FindByEmailAsync(dto.Email);
                    if (emailExists != null && emailExists.Id != userId)
                    {
                        return ServiceResult<UserProfileDto>.Failure("Email is already in use by another account");
                    }

                    user.Email = dto.Email;
                    user.UserName = dto.Email;
                    user.NormalizedEmail = _userManager.NormalizeEmail(dto.Email);
                    user.NormalizedUserName = _userManager.NormalizeName(dto.Email);
                }

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ServiceResult<UserProfileDto>.Failure($"Failed to update profile: {errors}");
                }

                return ServiceResult<UserProfileDto>.SuccessResult(MapToDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user profile with ID: {userId}");
                return ServiceResult<UserProfileDto>.Failure("Failed to update user profile");
            }
        }

        public async Task<ServiceResult<string>> UpdateProfilePictureAsync(UpdateUserProfileImageDto dto)
        {
            var userIdString = UserHelper.GetUserId();
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return ServiceResult<string>.Failure("User not authenticated");
            }

            try
            {
                // Validate file
                if (dto.file == null || dto.file.Length == 0)
                    return ServiceResult<string>.Failure("No file uploaded");

                if (dto.file.Length > 5 * 1024 * 1024) // 5MB limit
                    return ServiceResult<string>.Failure("File size exceeds 5MB limit");

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(dto.file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                    return ServiceResult<string>.Failure("Invalid file type. Only images are allowed");

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ServiceResult<string>.Failure("User not found");

                // Remove old picture if exists
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, user.ProfilePictureUrl.TrimStart('/'));
                    if (File.Exists(oldFilePath))
                    {
                        File.Delete(oldFilePath);
                    }
                }

                // Ensure directory exists (creates all parent folders if needed)
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profile-pictures");
                Directory.CreateDirectory(uploadsFolder); // ✅ Safe even if folder already exists

                // Generate unique filename
                var uniqueFileName = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.file.CopyToAsync(stream);
                }

                // Update user with new profile picture URL
                var relativePath = $"/uploads/profile-pictures/{uniqueFileName}";
                user.ProfilePictureUrl = relativePath;

                await _userManager.UpdateAsync(user);

                return ServiceResult<string>.SuccessResult(relativePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading profile picture for user with ID: {userId}");
                return ServiceResult<string>.Failure("Failed to upload profile picture");
            }
        }

        public async Task<ServiceResult<bool>> RemoveProfilePictureAsync()
        {
            var userIdString = UserHelper.GetUserId();
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return ServiceResult<bool>.Failure("User not authenticated");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ServiceResult<bool>.Failure("User not found");

                if (string.IsNullOrEmpty(user.ProfilePictureUrl))
                    return ServiceResult<bool>.SuccessResult(true); // No picture to remove

                // Delete the physical file
                var filePath = Path.Combine(_environment.WebRootPath, user.ProfilePictureUrl.TrimStart('/'));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Update user
                user.ProfilePictureUrl = null;

                await _userManager.UpdateAsync(user);

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing profile picture for user with ID: {userId}");
                return ServiceResult<bool>.Failure("Failed to remove profile picture");
            }
        }

        public async Task<ServiceResult<bool>> ChangePasswordAsync(fatortak.Dtos.UserProfile.ChangePasswordDto dto)
        {
            var userIdString = UserHelper.GetUserId();
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return ServiceResult<bool>.Failure("User not authenticated");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ServiceResult<bool>.Failure("User not found");

                // Verify current password
                var passwordValid = await _userManager.CheckPasswordAsync(user, dto.CurrentPassword);
                if (!passwordValid)
                    return ServiceResult<bool>.Failure("Current password is incorrect");

                // Change password
                var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ServiceResult<bool>.Failure($"Failed to change password: {errors}");
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user with ID: {userId}");
                return ServiceResult<bool>.Failure("Failed to change password");
            }
        }

        public async Task<ServiceResult<bool>> UpdateUserStatusAsync(Guid userId, bool isActive)
        {
            try
            {
                var currentUserIdString = UserHelper.GetUserId();
                if (string.IsNullOrEmpty(currentUserIdString))
                {
                    return ServiceResult<bool>.Failure("User not authenticated");
                }

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return ServiceResult<bool>.Failure("User not found");

                // Prevent users from deactivating themselves
                if (user.Id.ToString() == currentUserIdString)
                    return ServiceResult<bool>.Failure("You cannot change your own status");

                user.IsActive = isActive;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return ServiceResult<bool>.Failure($"Failed to update user status: {errors}");
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for user with ID: {userId}");
                return ServiceResult<bool>.Failure("Failed to update user status");
            }
        }

        private UserProfileDto MapToDto(ApplicationUser user) => new()
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            ProfilePictureUrl = GenerateImageWithFolderName(user.ProfilePictureUrl),
            Role = user.Role,
            IsActive = user.IsActive,
            TenantId = user.TenantId,
            TenantName = user.Tenant?.Name,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };


        private string GenerateImageWithFolderName(string? imageName)
        {
            var request = _httpContextAccessor?.HttpContext?.Request;
            return request != null && imageName != null
                ? $"{request.Scheme}://{request.Host.Value}{imageName}"
                : string.Empty;
        }
    }
}