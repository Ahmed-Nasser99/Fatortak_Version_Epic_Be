using fatortak.Context;
using Microsoft.EntityFrameworkCore;

namespace fatortak.Middlewares
{
    public class SubscriptionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SubscriptionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            // If no authorization header, pass through
            if (string.IsNullOrEmpty(authHeader))
            {
                await _next(context);
                return;
            }

            var user = context.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            var tenantIdClaim = user.FindFirst("tenant_id");
            if (tenantIdClaim == null || !Guid.TryParse(tenantIdClaim.Value, out var tenantId))
            {
                await _next(context);
                return;
            }

            var hasValidSub = await db.Subscriptions
                .AnyAsync(s => s.TenantId == tenantId && (s.EndDate == null || s.EndDate > DateTime.UtcNow));

            if (!hasValidSub)
            {
                context.Response.StatusCode = 430; // Custom code for "Subscription Required"
                await context.Response.WriteAsync("Subscription is missing or expired.");
                return;
            }

            await _next(context);
        }
    }

}
