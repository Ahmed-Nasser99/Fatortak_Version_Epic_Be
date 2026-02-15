using fatortak.Dtos.Shared;
using fatortak.Dtos.UserProfile;

namespace fatortak.Services.UserService
{
    public interface IUserProfileService
    {
        Task<ServiceResult<UserProfileDto>> GetCurrentUserProfileAsync();
        Task<ServiceResult<UserProfileDto>> GetUserProfileAsync(Guid userId);
        Task<ServiceResult<UserProfileDto>> UpdateUserProfileAsync(UserProfileUpdateDto dto);
        Task<ServiceResult<string>> UpdateProfilePictureAsync(UpdateUserProfileImageDto dto);
        Task<ServiceResult<bool>> RemoveProfilePictureAsync();
        Task<ServiceResult<bool>> ChangePasswordAsync(fatortak.Dtos.UserProfile.ChangePasswordDto dto);
        Task<ServiceResult<bool>> UpdateUserStatusAsync(Guid userId, bool isActive);
    }
}