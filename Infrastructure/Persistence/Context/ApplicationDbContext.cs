using GA.Core.Domain.Common;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
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

        // Çoklu Kiracı (Multi-Tenancy) ve Müşteri Yapısı
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Customer> Customers { get; set; }

        // Sistem Tabloları
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<FieldWorkerProfile> FieldWorkerProfiles { get; set; }

        // Menü ve Saha Operasyon Tabloları
        public DbSet<Survey> Surveys { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Timesheet> Timesheets { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<Station> Stations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // PostGIS yeteneklerini aktifleştiriyoruz
            modelBuilder.HasPostgresExtension("postgis");

            // 🚀 KÖKLÜ DÜZELTME: Çökmeye sebep olan Expression.Constant yapısı yerine
            // Sektör standardı olan dinamik generic filtre metodunu reflection ile tetikliyoruz.
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

            // Çoka Çok İlişki Tanımlamaları
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // User ile FieldWorkerProfile Bire Bir İlişkisi
            modelBuilder.Entity<User>()
                .HasOne(u => u.FieldWorkerProfile)
                .WithOne(f => f.User)
                .HasForeignKey<FieldWorkerProfile>(f => f.UserId);

            // Ekip/Personel Silinirse İş Emirlerini Koruma Kuralları
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
        }

        // 🚀 KÖKLÜ DÜZELTME: EF Core'un her istekte (request) canlı olarak çözebileceği,
        // Asla hafıza sızıntısı veya 500 hatası üretmeyen temiz kurumsal filtre motoru.
        private void ConfigureTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMultiTenant
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                _currentUserService.TenantId == Guid.Empty || e.TenantId == _currentUserService.TenantId);
        }

        // --- OTOMATİK VERİ DOLDURMA ---
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