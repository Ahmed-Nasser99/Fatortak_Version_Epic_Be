using fatortak.Dtos.Shared;
using fatortak.Dtos.User;

namespace fatortak.Services.UserService
{
    public interface IUserService
    {
        Task<ServiceResult<UserDto>> CreateUserAsync(UserCreateDto dto, Guid currentUserId);
        Task<ServiceResult<UserDto>> GetUserAsync(Guid userId);
        Task<ServiceResult<PagedResponseDto<UserDto>>> GetUsersAsync(UserFilterDto filter, PaginationDto pagination);
        Task<ServiceResult<UserDto>> UpdateUserAsync(Guid userId, UserUpdateDto dto, Guid currentUserId);
        Task<ServiceResult<bool>> DeleteUserAsync(Guid userId, Guid currentUserId);
        Task<ServiceResult<bool>> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
    }
}
