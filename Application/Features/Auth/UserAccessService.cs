using GA.Application.Features.Partners;
using GA.Core.Domain.Constants;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.Auth
{
    public class UserAccessService : IUserAccessService
    {
        private const string SuperAdminEmail = "admin@theobuz.com";

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public UserAccessService(ApplicationDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<bool> CanAccessOfficeChatInboxAsync(CancellationToken ct = default)
        {
            if (_currentUser.UserId == Guid.Empty) return false;
            if (_currentUser.TenantId == Guid.Empty) return true;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.FieldWorkerProfile)
                .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId && !u.IsDeleted, ct);

            if (user == null) return false;
            if (IsSuperAdminEmail(user.Email)) return true;

            var roles = user.UserRoles
                .Where(ur => ur.Role != null && !ur.Role.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (roles.Any(r => RoleNames.OfficeChatRoles.Contains(r)))
                return true;

            var hasFieldProfile = user.FieldWorkerProfile != null && !user.FieldWorkerProfile.IsDeleted;
            return !hasFieldProfile;
        }

        public async Task<bool> CanAccessOfficeDirectChatAsync(CancellationToken ct = default)
        {
            if (_currentUser.UserId == Guid.Empty) return false;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId && !u.IsDeleted, ct);

            if (user == null) return false;
            if (IsSuperAdminEmail(user.Email)) return true;

            var roles = user.UserRoles
                .Where(ur => ur.Role != null && !ur.Role.IsDeleted)
                .Select(ur => ur.Role.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return roles.Any(r => RoleNames.OfficeDirectChatRoles.Contains(r));
        }

        public async Task<bool> IsFieldWorkerOnlyForChatAsync(CancellationToken ct = default)
        {
            if (_currentUser.UserId == Guid.Empty) return false;
            if (await CanAccessOfficeChatInboxAsync(ct)) return false;

            return await _context.FieldWorkerProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(p => p.UserId == _currentUser.UserId && !p.IsDeleted, ct);
        }

        public async Task<bool> CanUseMobileOperationsChatAsync(CancellationToken ct = default)
        {
            if (_currentUser.UserId == Guid.Empty) return false;

            return await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(u => u.Id == _currentUser.UserId && !u.IsDeleted, ct);
        }

        public async Task<bool> IsSuperAdminAsync(CancellationToken ct = default)
        {
            if (_currentUser.UserId == Guid.Empty) return false;
            if (_currentUser.TenantId == Guid.Empty) return true;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId && !u.IsDeleted, ct);

            if (user != null && IsSuperAdminEmail(user.Email))
                return true;

            var roles = await GetRoleNamesAsync(_currentUser.UserId, ct);
            return roles.Any(r => string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> IsTenantAdminOrAboveAsync(CancellationToken ct = default)
        {
            if (await IsSuperAdminAsync(ct)) return true;
            if (_currentUser.UserId == Guid.Empty) return false;

            var roles = await GetRoleNamesAsync(_currentUser.UserId, ct);
            return roles.Any(r => string.Equals(r, RoleNames.TenantAdmin, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default)
        {
            return await _context.UserRoles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Join(
                    _context.Roles.IgnoreQueryFilters().AsNoTracking().Where(r => !r.IsDeleted),
                    ur => ur.RoleId,
                    r => r.Id,
                    (_, r) => r.Name)
                .Distinct()
                .ToListAsync(ct);
        }

        public async Task<bool> IsOperationReporterOnlyAsync(CancellationToken ct = default)
        {
            var roles = await GetRoleNamesAsync(_currentUser.UserId, ct);
            return roles.Any(r => string.Equals(r, RoleNames.OperationReporter, StringComparison.OrdinalIgnoreCase))
                   && !roles.Any(r =>
                       string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(r, RoleNames.TenantAdmin, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(r, RoleNames.OfficeUser, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> IsIsgInspectorAsync(CancellationToken ct = default)
        {
            if (await IsSuperAdminAsync(ct)) return false;
            var roles = await GetRoleNamesAsync(_currentUser.UserId, ct);
            return roles.Any(r => string.Equals(r, RoleNames.IsgInspector, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> CanViewIsgPhotosAsync(CancellationToken ct = default)
        {
            if (await IsSuperAdminAsync(ct)) return true;
            if (await IsIsgInspectorAsync(ct)) return true;
            if (await IsTenantAdminOrAboveAsync(ct)) return false;
            return true;
        }

        public async Task<bool> CanViewOperationPhotosAsync(CancellationToken ct = default)
        {
            if (await IsSuperAdminAsync(ct)) return true;
            if (await IsIsgInspectorAsync(ct)) return false;
            return true;
        }

        public async Task<bool> CanManageEdasCompaniesAsync(CancellationToken ct = default)
        {
            if (await IsSuperAdminAsync(ct)) return true;
            return _currentUser.TenantId == PartnerCatalog.SeedTenantIds.Trugo;
        }

        private static bool IsSuperAdminEmail(string? email) =>
            string.Equals(email, SuperAdminEmail, StringComparison.OrdinalIgnoreCase);
    }
}
