using fatortak.Common.Enum;
using fatortak.Context;

namespace fatortak.Helpers
{
    public static class UserHelper
    {
        private static IHttpContextAccessor _httpContextAccessor;

        // This method is called to set the IHttpContextAccessor from DI
        public static void Configure(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Method to get the current user's username
        public static string GetUserName()
        {
            var claimsPrincipal = _httpContextAccessor?.HttpContext?.User;

            if (claimsPrincipal != null)
            {
                return claimsPrincipal.FindFirst("UserName")?.Value;
            }

            return null; // Or handle accordingly if user is not found
        }


        public static string GetUserNameByUserId(string? userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                var _AppContext = _httpContextAccessor.HttpContext.RequestServices.GetService<ApplicationDbContext>();
                var user = _AppContext.Users.FirstOrDefault(u => u.Id.Equals(userId));
                if (user != null)
                {
                    return user?.UserName;
                }
                return null;
            }

            return null; // Or handle accordingly if user is not found
        }

        // Method to get the current user's userId
        public static string GetUserId()
        {
            var claimsPrincipal = _httpContextAccessor?.HttpContext?.User;

            if (claimsPrincipal != null)
            {
                return claimsPrincipal.FindFirst("UserId")?.Value;
            }

            return null; // Or handle accordingly if user is not found
        }
        public static bool IaSysAdminUser()
        {
            var claimsPrincipal = _httpContextAccessor?.HttpContext?.User;
            string stringUserId = string.Empty;

            if (claimsPrincipal != null)
            {
                stringUserId = claimsPrincipal.FindFirst("UserId")?.Value;
            }
            else
            {
                return false;
            }

            if (!string.IsNullOrEmpty(stringUserId) && Guid.TryParse(stringUserId, out var userId))
            {
                var _AppContext = _httpContextAccessor.HttpContext.RequestServices.GetService<ApplicationDbContext>();
                var user = _AppContext.Users.FirstOrDefault(u => u.Id.Equals(userId));
                if (user != null)
                {
                    return user.Role.Equals(RoleEnum.SysAdmin.ToString());
                }
                return false;
            }

            return false;
        }

    }
}
