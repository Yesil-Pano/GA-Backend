using GA.Application.Features.Users.DTOs;
using GA.Application.Features.WorkOrders;
using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GA.Application.Features.Users
{
    public class UserManagementService : IUserManagementService
    {
        private readonly ApplicationDbContext _context;

        public UserManagementService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ManagedUserResultDto> CreateUserAsync(CreateManagedUserDto dto, CancellationToken ct = default)
        {
            ValidateRequiredFields(dto.Username, dto.Email, dto.FullName, dto.PhoneNumber, dto.Password, isCreate: true);

            var requestedRoles = NormalizeRoleNames(dto.RoleNames);
            if (requestedRoles.Any(r => string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("SuperAdmin rolü yeni kullanıcılara atanamaz.");

            if (requestedRoles.Count == 0)
                throw new InvalidOperationException("En az bir rol seçilmelidir.");

            await EnsureUniqueCredentialsAsync(dto.Email, dto.Username, excludeUserId: null, ct);

            var tenantId = await ResolveTenantIdAsync(requestedRoles, dto.TenantId, ct);
            var validRoles = await LoadValidRolesAsync(requestedRoles, ct);

            var user = new User
            {
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                IsActive = dto.IsActive,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);

            await ApplyRolesAsync(user.Id, validRoles, ct);
            await EnsureFieldWorkerProfileAsync(user.Id, tenantId, requestedRoles, ct);

            return await MapResultAsync(user.Id, ct);
        }

        public async Task<ManagedUserResultDto> UpdateUserAsync(Guid userId, UpdateManagedUserDto dto, CancellationToken ct = default)
        {
            ValidateRequiredFields(dto.Username, dto.Email, dto.FullName, dto.PhoneNumber, dto.Password, isCreate: false);

            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

            var existingRoleNames = await GetUserRoleNamesAsync(userId, ct);
            var hadSuperAdmin = existingRoleNames.Any(r =>
                string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase));

            var requestedRoles = NormalizeRoleNames(dto.RoleNames);
            var wantsSuperAdmin = requestedRoles.Any(r =>
                string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase));

            if (wantsSuperAdmin && !hadSuperAdmin)
                throw new InvalidOperationException("SuperAdmin rolü atanamaz.");

            if (hadSuperAdmin && !wantsSuperAdmin)
                requestedRoles.Add(RoleNames.SuperAdmin);

            if (requestedRoles.Count == 0)
                throw new InvalidOperationException("En az bir rol seçilmelidir.");

            await EnsureUniqueCredentialsAsync(dto.Email, dto.Username, excludeUserId: userId, ct);

            var tenantId = await ResolveTenantIdAsync(requestedRoles, dto.TenantId, ct);
            var validRoles = await LoadValidRolesAsync(requestedRoles, ct);

            user.Username = dto.Username.Trim();
            user.Email = dto.Email.Trim();
            user.FullName = dto.FullName.Trim();
            user.PhoneNumber = dto.PhoneNumber.Trim();
            user.IsActive = dto.IsActive;
            user.TenantId = tenantId;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(dto.Password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            await ApplyRolesAsync(user.Id, validRoles, ct);
            await EnsureFieldWorkerProfileAsync(user.Id, tenantId, requestedRoles, ct);
            await SyncFieldWorkerProfileTenantAsync(user.Id, tenantId, ct);

            await _context.SaveChangesAsync(ct);

            return await MapResultAsync(user.Id, ct);
        }

        public async Task DeleteUserAsync(Guid userId, Guid actorUserId, CancellationToken ct = default)
        {
            if (userId == actorUserId)
                throw new InvalidOperationException("Kendi hesabınızı silemezsiniz.");

            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

            var roleNames = await GetUserRoleNamesAsync(userId, ct);
            if (roleNames.Any(r => string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Super Admin hesabı silinemez.");

            user.IsDeleted = true;
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.FieldWorkerProfile != null && !user.FieldWorkerProfile.IsDeleted)
            {
                user.FieldWorkerProfile.IsDeleted = true;
                user.FieldWorkerProfile.UpdatedAt = DateTime.UtcNow;
            }

            var openStatuses = new[]
            {
                WorkOrderStatus.Unassigned,
                WorkOrderStatus.Waiting,
                WorkOrderStatus.InProgress,
            };

            var openOrders = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted
                            && w.AssignedToUserId == userId
                            && openStatuses.Contains(w.Status))
                .ToListAsync(ct);

            foreach (var order in openOrders)
            {
                order.AssignedToUserId = null;
                WorkOrderStatus.ApplyOnUnassign(order);
            }

            await _context.SaveChangesAsync(ct);
        }

        private static void ValidateRequiredFields(
            string username,
            string email,
            string fullName,
            string phoneNumber,
            string? password,
            bool isCreate)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Kullanıcı adı zorunludur.");
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("E-posta zorunludur.");
            if (string.IsNullOrWhiteSpace(fullName))
                throw new InvalidOperationException("Ad soyad zorunludur.");
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new InvalidOperationException("Telefon numarası zorunludur.");
            if (isCreate && string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Şifre zorunludur.");
        }

        private static List<string> NormalizeRoleNames(IEnumerable<string>? roleNames) =>
            (roleNames ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        private async Task EnsureUniqueCredentialsAsync(
            string email,
            string username,
            Guid? excludeUserId,
            CancellationToken ct)
        {
            var normalizedEmail = email.Trim();
            var normalizedUsername = username.Trim();

            var emailTaken = await _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => !u.IsDeleted
                               && u.Email.ToLower() == normalizedEmail.ToLower()
                               && (excludeUserId == null || u.Id != excludeUserId), ct);
            if (emailTaken)
                throw new InvalidOperationException("Bu e-posta adresi sistemde zaten kayıtlı.");

            var usernameTaken = await _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => !u.IsDeleted
                               && u.Username.ToLower() == normalizedUsername.ToLower()
                               && (excludeUserId == null || u.Id != excludeUserId), ct);
            if (usernameTaken)
                throw new InvalidOperationException("Bu kullanıcı adı sistemde zaten kayıtlı.");
        }

        private async Task<Guid> ResolveTenantIdAsync(
            IReadOnlyList<string> roleNames,
            Guid requestedTenantId,
            CancellationToken ct)
        {
            var isSuperAdminUser = roleNames.Any(r =>
                string.Equals(r, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase));

            if (isSuperAdminUser)
                return Guid.Empty;

            if (requestedTenantId == Guid.Empty)
                throw new InvalidOperationException("Firma seçimi zorunludur.");

            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == requestedTenantId && !t.IsDeleted, ct);

            if (tenant == null)
                throw new InvalidOperationException("Seçilen firma bulunamadı.");
            if (!tenant.IsActive)
                throw new InvalidOperationException("Seçilen firma pasif durumda.");

            return requestedTenantId;
        }

        private async Task<List<Role>> LoadValidRolesAsync(IReadOnlyList<string> requested, CancellationToken ct)
        {
            var validRoles = await _context.Roles
                .IgnoreQueryFilters()
                .Where(r => !r.IsDeleted && requested.Contains(r.Name))
                .ToListAsync(ct);

            if (validRoles.Count != requested.Count)
                throw new InvalidOperationException("Geçersiz rol adı içeriyor.");

            return validRoles;
        }

        private async Task ApplyRolesAsync(Guid userId, IReadOnlyList<Role> roles, CancellationToken ct)
        {
            var existing = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .ToListAsync(ct);

            _context.UserRoles.RemoveRange(existing);
            foreach (var role in roles)
            {
                _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task EnsureFieldWorkerProfileAsync(
            Guid userId,
            Guid tenantId,
            IReadOnlyList<string> roleNames,
            CancellationToken ct)
        {
            var needsProfile = roleNames.Any(r =>
                string.Equals(r, RoleNames.FieldWorker, StringComparison.OrdinalIgnoreCase));
            if (!needsProfile) return;

            var profile = await _context.FieldWorkerProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, ct);

            if (profile != null)
            {
                if (profile.TenantId != tenantId)
                {
                    profile.TenantId = tenantId;
                    profile.UpdatedAt = DateTime.UtcNow;
                }
                return;
            }

            _context.FieldWorkerProfiles.Add(new FieldWorkerProfile
            {
                UserId = userId,
                TenantId = tenantId,
                ProjectName = "-",
                VehiclePlate = "-",
                TeamLeader = "-",
                HomeLocation = new Point(32.85411, 39.92077) { SRID = 4326 },
                CreatedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync(ct);
        }

        private async Task SyncFieldWorkerProfileTenantAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            var profile = await _context.FieldWorkerProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, ct);

            if (profile == null || profile.TenantId == tenantId) return;

            profile.TenantId = tenantId;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        private async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(Guid userId, CancellationToken ct) =>
            await _context.UserRoles
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

        private async Task<ManagedUserResultDto> MapResultAsync(Guid userId, CancellationToken ct)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstAsync(u => u.Id == userId, ct);

            var roles = await GetUserRoleNamesAsync(userId, ct);

            return new ManagedUserResultDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                TenantId = user.TenantId,
                IsActive = user.IsActive,
                Roles = roles.ToList(),
            };
        }
    }
}
