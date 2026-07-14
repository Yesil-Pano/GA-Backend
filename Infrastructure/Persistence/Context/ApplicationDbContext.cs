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
        public DbSet<Project> Projects { get; set; } = null!;
        public DbSet<Photo> Photos { get; set; } = null!;
        public DbSet<City> Cities { get; set; } = null!;
        public DbSet<District> Districts { get; set; } = null!;

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

            // İl / İlçe — kaynak Cities/Districts şeması ile uyumlu (cross-server COPY)
            modelBuilder.Entity<City>(entity =>
            {
                entity.ToTable("Cities");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
                entity.Property(c => c.Latitude).HasMaxLength(50).IsRequired();
                entity.Property(c => c.Longitude).HasMaxLength(50).IsRequired();
                entity.Property(c => c.Createdby).HasMaxLength(100).IsRequired();
                entity.Property(c => c.Updatedby).HasMaxLength(100).IsRequired();
                entity.HasIndex(c => c.Name).IsUnique().HasDatabaseName("IX_Cities_Name");
            });

            modelBuilder.Entity<District>(entity =>
            {
                entity.ToTable("Districts");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
                entity.Property(d => d.Latitude).HasMaxLength(50).IsRequired();
                entity.Property(d => d.Longitude).HasMaxLength(50).IsRequired();
                entity.Property(d => d.Createdby).HasMaxLength(100).IsRequired();
                entity.Property(d => d.Updatedby).HasMaxLength(100).IsRequired();
                entity.HasIndex(d => d.CityId).HasDatabaseName("IX_Districts_CityId");
                entity.HasOne(d => d.City)
                    .WithMany(c => c.Districts)
                    .HasForeignKey(d => d.CityId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Districts_Cities_CityId");
            });

            modelBuilder.Entity<WorkOrder>(entity =>
            {
                entity.HasOne(w => w.CityRef)
                    .WithMany()
                    .HasForeignKey(w => w.CityId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(w => w.DistrictRef)
                    .WithMany()
                    .HasForeignKey(w => w.DistrictId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(w => w.CityId);
                entity.HasIndex(w => w.DistrictId);
            });

            modelBuilder.Entity<Station>(entity =>
            {
                entity.HasOne(s => s.CityRef)
                    .WithMany()
                    .HasForeignKey(s => s.CityId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(s => s.DistrictRef)
                    .WithMany()
                    .HasForeignKey(s => s.DistrictId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(s => s.CityId);
                entity.HasIndex(s => s.DistrictId);
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