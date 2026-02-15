using fatortak.Context;
using fatortak.Entities;
using fatortak.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace fatortak.Middlewares
{
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
             UserManager<ApplicationUser> _userManager,
            ApplicationDbContext dbContext)
        {

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            // If no authorization header, pass through
            if (string.IsNullOrEmpty(authHeader))
            {
                await _next(context);
                return;
            }
            // Try to get tenant from subdomain first
            var host = context.Request.Host.Host;
            var subdomain = host.Split('.')[0];

            // Or from JWT claim
            var tenantIdClaim = context.User?.FindFirst("tenant_id");

            Tenant tenant = null;

            if (UserHelper.IaSysAdminUser())
            {
                await _next(context);
                return;
            }

            if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId))
            {
                tenant = await dbContext.Tenants.FindAsync(tenantId);
            }

            if (tenant != null)
            {
                // Verify user has access to this tenant if authenticated
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                    {
                        var hasAccess = await _userManager.Users
                            .AnyAsync(tu => tu.TenantId == tenant.Id &&
                                          tu.Id == userGuid &&
                                          tu.IsActive);

                        if (!hasAccess)
                        {
                            context.Response.StatusCode = 403;
                            await context.Response.WriteAsync("Access to tenant denied");
                            return;
                        }
                    }
                }

                context.Items["CurrentTenant"] = tenant;
            }
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Tenant not found");
                return;
            }

            await _next(context);
        }
    }
}
