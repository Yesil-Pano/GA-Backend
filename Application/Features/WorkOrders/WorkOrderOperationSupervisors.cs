using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.WorkOrders
{
    /// <summary>Yeşil Pano operasyon sorumlusu seçimi (Trugo arıza açılışı).</summary>
    public static class WorkOrderOperationSupervisors
    {
        public static readonly Guid YesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");

        private static readonly Guid[] OpsRoleIds =
        {
            RoleNames.TenantAdminRoleId,
            RoleNames.OfficeUserRoleId,
        };

        public static IQueryable<User> QueryYesilPanoSupervisors(ApplicationDbContext context) =>
            context.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted
                            && u.IsActive
                            && u.TenantId == YesilPanoTenantId
                            && context.UserRoles.Any(ur =>
                                ur.UserId == u.Id
                                && OpsRoleIds.Contains(ur.RoleId)
                                && context.Roles.Any(r =>
                                    r.Id == ur.RoleId && !r.IsDeleted)));

        public static Task<bool> IsValidYesilPanoSupervisorAsync(
            ApplicationDbContext context,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            QueryYesilPanoSupervisors(context)
                .AnyAsync(u => u.Id == userId, cancellationToken);
    }
}
