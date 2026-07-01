using GA.Core.Domain.Common;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GA.Infrastructure.Persistence.Context
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ICurrentUserService _currentUserService;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        public Guid CurrentTenantId
        {
            get
            {
                try
                {
                    return _currentUserService?.TenantId ?? Guid.Empty;
                }
                catch
                {
                    return Guid.Empty;
                }
            }
        }

        // 🔒 DÜZELTME: CS8618 uyarısını engellemek için tüm DbSet alanlarına '= null!;' mühürleri vuruldu.

        // Çoklu Kiracı (Multi-Tenancy) ve Müşteri Yapısı
        public DbSet<Tenant> Tenants { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;

        // Sistem Tabloları
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<UserRole> UserRoles { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<FieldWorkerProfile> FieldWorkerProfiles { get; set; } = null!;

        // Menü ve Saha Operasyon Tabloları
        public DbSet<Survey> Surveys { get; set; } = null!;
        public DbSet<Warehouse> Warehouses { get; set; } = null!;
        public DbSet<Timesheet> Timesheets { get; set; } = null!;
        public DbSet<WorkOrder> WorkOrders { get; set; } = null!;
        public DbSet<Station> Stations { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!; // 🚀 Yeni projesel SaaS tablomuz

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(ApplicationDbContext)
                        .GetMethod(nameof(ConfigureTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.MakeGenericMethod(entityType.ClrType);

                    method?.Invoke(this, new object[] { modelBuilder });
                }
            }

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<User>()
                .HasOne(u => u.FieldWorkerProfile)
                .WithOne(f => f.User)
                .HasForeignKey<FieldWorkerProfile>(f => f.UserId);

            modelBuilder.Entity<WorkOrder>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(w => w.OperationUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkOrder>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(w => w.OpenedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkOrder>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(w => w.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // 🚀 ÇOKA-ÇOK İLİŞKİ YAPILANDIRMASI: FieldWorkerProfiles <-> Projects
            modelBuilder.Entity<FieldWorkerProfile>()
                .HasMany(f => f.Projects)
                .WithMany(p => p.FieldWorkerProfiles)
                .UsingEntity<Dictionary<string, object>>(
                    "FieldWorkerProfileProjects",
                    j => j.HasOne<Project>().WithMany().HasForeignKey("ProjectId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<FieldWorkerProfile>().WithMany().HasForeignKey("FieldWorkerProfileId").OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("FieldWorkerProfileId", "ProjectId");
                    });
        }

        private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMultiTenant
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<IMultiTenant>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        if (entry.Entity.TenantId == Guid.Empty)
                        {
                            entry.Entity.TenantId = _currentUserService.TenantId;
                            entry.Entity.CustomerId = _currentUserService.CustomerId;
                        }
                        break;
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}