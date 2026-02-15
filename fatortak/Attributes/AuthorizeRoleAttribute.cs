using fatortak.Common.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace fatortak.Attributes
{
    public class AuthorizeRoleAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly RoleEnum[] _allowedRoles;

        public AuthorizeRoleAttribute(params RoleEnum[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            var user = context.HttpContext.User;
            if (!user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Get user's role from claims
            var userRoleClaim = user.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userRoleClaim))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Try to parse the role string to enum
            if (!Enum.TryParse<RoleEnum>(userRoleClaim, out var userRole))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Check if user has required role
            if (!_allowedRoles.Contains(userRole))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}