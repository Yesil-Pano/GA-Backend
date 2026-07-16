using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Infrastructure.Persistence.Seed
{
    public static class RoleSeeder
    {
        private const string SuperAdminEmail = "admin@theobuz.com";
        private static readonly DateTime SeedTime = new(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);

        public static async Task SeedAsync(ApplicationDbContext context, CancellationToken ct = default)
        {
            await EnsureRolesAsync(context, ct);
            await AssignDefaultUserRolesAsync(context, ct);
        }

        private static async Task EnsureRolesAsync(ApplicationDbContext context, CancellationToken ct)
        {
            var roles = new[]
            {
                new Role
                {
                    Id = RoleNames.SuperAdminRoleId,
                    Name = RoleNames.SuperAdmin,
                    Description = "Sistem yöneticisi — tüm firmalar",
                    CreatedAt = SeedTime,
                },
                new Role
                {
                    Id = RoleNames.TenantAdminRoleId,
                    Name = RoleNames.TenantAdmin,
                    Description = "Firma yöneticisi — kendi tenant sohbet gelen kutusu",
                    CreatedAt = SeedTime,
                },
                new Role
                {
                    Id = RoleNames.OfficeUserRoleId,
                    Name = RoleNames.OfficeUser,
                    Description = "Ofis / operasyon kullanıcısı",
                    CreatedAt = SeedTime,
                },
                new Role
                {
                    Id = RoleNames.FieldWorkerRoleId,
                    Name = RoleNames.FieldWorker,
                    Description = "Saha personeli — mobil sohbet",
                    CreatedAt = SeedTime,
                },
            };

            foreach (var role in roles)
            {
                var exists = await context.Roles
                    .IgnoreQueryFilters()
                    .AnyAsync(r => r.Id == role.Id || r.Name == role.Name, ct);
                if (!exists)
                    context.Roles.Add(role);
            }

            await context.SaveChangesAsync(ct);
        }

        private static async Task AssignDefaultUserRolesAsync(ApplicationDbContext context, CancellationToken ct)
        {
            var roleIds = await context.Roles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => !r.IsDeleted)
                .ToDictionaryAsync(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase, ct);

            var users = await context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                .Include(u => u.UserRoles)
                .Where(u => !u.IsDeleted)
                .ToListAsync(ct);

            foreach (var user in users)
            {
                if (user.UserRoles.Count > 0)
                    continue;

                Guid roleId;
                if (string.Equals(user.Email, SuperAdminEmail, StringComparison.OrdinalIgnoreCase))
                {
                    roleId = roleIds[RoleNames.SuperAdmin];
                }
                else if (user.FieldWorkerProfile != null && !user.FieldWorkerProfile.IsDeleted)
                {
                    roleId = roleIds[RoleNames.FieldWorker];
                }
                else
                {
                    roleId = roleIds[RoleNames.TenantAdmin];
                }

                context.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId,
                });
            }

            // Süper admin hem profili olsa bile ofis erişimi için SuperAdmin rolü taşımalı
            var admin = users.FirstOrDefault(u =>
                string.Equals(u.Email, SuperAdminEmail, StringComparison.OrdinalIgnoreCase));
            if (admin != null && roleIds.TryGetValue(RoleNames.SuperAdmin, out var superAdminRoleId))
            {
                var hasSuperAdmin = admin.UserRoles.Any(ur => ur.RoleId == superAdminRoleId);
                if (!hasSuperAdmin)
                {
                    context.UserRoles.Add(new UserRole
                    {
                        UserId = admin.Id,
                        RoleId = superAdminRoleId,
                    });
                }
            }

            await context.SaveChangesAsync(ct);
        }
    }
}
