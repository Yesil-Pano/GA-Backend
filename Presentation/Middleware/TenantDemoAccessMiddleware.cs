using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GA.Presentation.Middleware
{
    /// <summary>
    /// Demo süresi dolmuş tenant kullanıcılarının API erişimini keser (web + mobil).
    /// SuperAdmin (TenantId = Empty) muaf.
    /// </summary>
    public class TenantDemoAccessMiddleware
    {
        private static readonly PathString AuthLoginPath = new("/api/auth/login");
        private static readonly PathString AuthRegisterPath = new("/api/auth/register");

        private readonly RequestDelegate _next;

        public TenantDemoAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext db,
            IMemoryCache cache)
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments(AuthLoginPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments(AuthRegisterPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var tenantClaim = context.User.FindFirst("TenantId")?.Value;
                if (Guid.TryParse(tenantClaim, out var tenantId) && tenantId != Guid.Empty)
                {
                    var status = await cache.GetOrCreateAsync(
                        $"tenant-access:{tenantId:D}",
                        async entry =>
                        {
                            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45);
                            var row = await db.Tenants
                                .AsNoTracking()
                                .IgnoreQueryFilters()
                                .Where(t => t.Id == tenantId)
                                .Select(t => new TenantAccessSnapshot(
                                    t.IsDeleted,
                                    t.IsActive,
                                    t.IsDemo,
                                    t.DemoExpiresAt))
                                .FirstOrDefaultAsync();
                            return row ?? new TenantAccessSnapshot(true, false, false, null);
                        }) ?? new TenantAccessSnapshot(true, false, false, null);

                    if (status.IsDeleted || !status.IsActive)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            message = "Firma hesabı pasif veya silinmiş. Erişim engellendi.",
                            code = "TENANT_INACTIVE",
                        });
                        return;
                    }

                    if (status.IsDemo
                        && status.DemoExpiresAt.HasValue
                        && status.DemoExpiresAt.Value <= DateTime.UtcNow)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            message = "Demo süreniz dolmuştur. Web ve mobil erişim kapatıldı. Lütfen yöneticinizle iletişime geçin.",
                            code = "DEMO_EXPIRED",
                            demoExpiresAt = status.DemoExpiresAt.Value.ToString("yyyy-MM-dd HH:mm"),
                        });
                        return;
                    }
                }
            }

            await _next(context);
        }

        private sealed record TenantAccessSnapshot(
            bool IsDeleted,
            bool IsActive,
            bool IsDemo,
            DateTime? DemoExpiresAt);
    }
}
