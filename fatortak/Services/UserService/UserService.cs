using fatortak.Common.Enum;
using fatortak.Context;
using fatortak.Dtos.Shared;
using fatortak.Dtos.User;
using fatortak.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Services.UserService
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UserService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            ILogger<UserService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid TenantId =>
            ((Tenant)_httpContextAccessor.HttpContext.Items["CurrentTenant"]).Id;

        public async Task<ServiceResult<UserDto>> CreateUserAsync(UserCreateDto dto, Guid currentUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Verify current user has permission to create users
                var currentUser = await _userManager.FindByIdAsync(currentUserId.ToString());
                if (currentUser == null || currentUser.Role != RoleEnum.Admin.ToString())
                    return ServiceResult<UserDto>.Failure("Unauthorized");

                // Check if email exists
                if (await _userManager.FindByEmailAsync(dto.Email) != null)
                    return ServiceResult<UserDto>.Failure("Email already registered");

                // Create user
                var user = new ApplicationUser
                {
                    UserName = dto.Email,
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    TenantId = TenantId, // Assign to current tenant
                    PhoneNumber = dto.PhoneNumber,
                    Role = dto.Role ?? RoleEnum.Watcher.ToString(),
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(user, dto.Password);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    return ServiceResult<UserDto>.ValidationError(errors);
                }

                await transaction.CommitAsync();

                return ServiceResult<UserDto>.SuccessResult(MapToDto(user, dto.Role));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating user {dto.Email}");
                return ServiceResult<UserDto>.Failure("Failed to create user");
            }
        }

        public async Task<ServiceResult<UserDto>> GetUserAsync(Guid userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.TenantId != TenantId)
                    return ServiceResult<UserDto>.Failure("User not found");

                var role = user.Role ?? "User";

                return ServiceResult<UserDto>.SuccessResult(MapToDto(user, role));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user {userId}");
                return ServiceResult<UserDto>.Failure("Failed to retrieve user");
            }
        }

        public async Task<ServiceResult<PagedResponseDto<UserDto>>> GetUsersAsync(
            UserFilterDto filter, PaginationDto pagination)
        {
            try
            {
                var query = _userManager.Users
                    .Where(u => u.TenantId == TenantId)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(filter.Email))
                    query = query.Where(u => u.Email.Contains(filter.Email));

                if (filter.IsActive.HasValue)
                    query = query.Where(u => u.IsActive == filter.IsActive.Value);

                // Join with TenantUsers to filter by role
                if (!string.IsNullOrWhiteSpace(filter.Role))
                {
                    query = query.Where(u => u.Role == filter.Role);
                }

                var totalCount = await query.CountAsync();

                var users = await query
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                    .Take(pagination.PageSize)
                    .ToListAsync();

                var userDtos = new List<UserDto>();
                foreach (var user in users)
                {
                    userDtos.Add(MapToDto(user, user.Role ?? "User"));
                }

                return ServiceResult<PagedResponseDto<UserDto>>.SuccessResult(
                    new PagedResponseDto<UserDto>
                    {
                        Data = userDtos,
                        PageNumber = pagination.PageNumber,
                        PageSize = pagination.PageSize,
                        TotalCount = totalCount
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return ServiceResult<PagedResponseDto<UserDto>>.Failure("Failed to retrieve users");
            }
        }

        public async Task<ServiceResult<UserDto>> UpdateUserAsync(
            Guid userId, UserUpdateDto dto, Guid currentUserId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Verify current user has permission to update users
                var currentUser = await _userManager.FindByIdAsync(currentUserId.ToString());
                if (currentUser == null || currentUser.Role != RoleEnum.Admin.ToString())
                    return ServiceResult<UserDto>.Failure("Unauthorized");

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.TenantId != TenantId)
                    return ServiceResult<UserDto>.Failure("User not found");

                // Update basic info
                if (!string.IsNullOrWhiteSpace(dto.FirstName))
                    user.FirstName = dto.FirstName;

                if (!string.IsNullOrWhiteSpace(dto.LastName))
                    user.LastName = dto.LastName;

                if (!string.IsNullOrWhiteSpace(dto.Email))
                    user.Email = dto.Email;

                if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                    user.PhoneNumber = dto.PhoneNumber;

                // Update role if specified
                if (!string.IsNullOrWhiteSpace(dto.Role))
                {
                    user.Role = dto.Role;
                }

                await _userManager.UpdateAsync(user);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var roles = await _userManager.GetRolesAsync(user);
                return ServiceResult<UserDto>.SuccessResult(MapToDto(user, user.Role ?? RoleEnum.Watcher.ToString()));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating user {userId}");
                return ServiceResult<UserDto>.Failure("Failed to update user");
            }
        }

        public async Task<ServiceResult<bool>> DeleteUserAsync(Guid userId, Guid currentUserId)
        {
            try
            {
                var currentUser = await _userManager.FindByIdAsync(currentUserId.ToString());
                if (currentUser == null || !(currentUser.Role == "Admin"))
                    return ServiceResult<bool>.Failure("Unauthorized");

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.TenantId != TenantId)
                    return ServiceResult<bool>.Failure("User not found");

                // Don't allow deactivating yourself
                if (user.Id == currentUserId)
                    return ServiceResult<bool>.Failure("Cannot deactivate yourself");

                await _userManager.DeleteAsync(user);

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating user {userId}");
                return ServiceResult<bool>.Failure("Failed to deactivate user");
            }
        }

        public async Task<ServiceResult<bool>> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.TenantId != TenantId)
                    return ServiceResult<bool>.Failure("User not found");

                var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    return ServiceResult<bool>.ValidationError(errors);
                }

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user {userId}");
                return ServiceResult<bool>.Failure("Failed to change password");
            }
        }

        private UserDto MapToDto(ApplicationUser user, string role) => new()
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }
}
