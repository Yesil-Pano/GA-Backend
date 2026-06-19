using GA.Core.Domain.Common;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

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

        // Çoklu Kiracı (Multi-Tenancy) ve Müşteri (Customer) Yapısı
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Customer> Customers { get; set; }

        // Tablolarımız
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<FieldWorkerProfile> FieldWorkerProfiles { get; set; }

        // Yeni eklenen tablolar (menü ile ilgili)
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

            // --- GLOBAL SORGULAR İÇİN GÜVENLİK FİLTRESİ ---
            // Sistemdeki IMultiTenant arayüzüne sahip tüm tabloları bulur ve otomatik filtre ekler
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(ConvertFilterExpression(entityType.ClrType));
                }
            }

            // Çoka Çok (Many-to-Many) İlişki Tanımlamaları
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            // User ile FieldWorkerProfile Bire Bir (1-1) İlişkisi
            modelBuilder.Entity<User>()
                .HasOne(u => u.FieldWorkerProfile)
                .WithOne(f => f.User)
                .HasForeignKey<FieldWorkerProfile>(f => f.UserId);

            
        }

        // Dinamik filtre oluşturucu asistan metot
        private System.Linq.Expressions.LambdaExpression ConvertFilterExpression(Type entityType)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
            var tenantIdProperty = System.Linq.Expressions.Expression.Property(parameter, "TenantId");

            // 💡 DÜZELTME: Expression.Property yerine Expression.Field kullanarak hatayı kökten çözüyoruz!
            var currentUserServiceField = System.Linq.Expressions.Expression.Field(System.Linq.Expressions.Expression.Constant(this), nameof(_currentUserService));
            var tenantIdValue = System.Linq.Expressions.Expression.Property(currentUserServiceField, nameof(ICurrentUserService.TenantId));

            // Normal Kullanıcı Kontrolü: e.TenantId == currentTenantId
            var compareTenant = System.Linq.Expressions.Expression.Equal(tenantIdProperty, tenantIdValue);

            // Tanrı Modu Kontrolü: currentTenantId == Guid.Empty
            var emptyGuid = System.Linq.Expressions.Expression.Constant(Guid.Empty);
            var isSuperAdmin = System.Linq.Expressions.Expression.Equal(tenantIdValue, emptyGuid);

            // İkisinden biri doğruysa veriyi getir (isSuperAdmin OR compareTenant)
            var orCondition = System.Linq.Expressions.Expression.OrElse(isSuperAdmin, compareTenant);

            return System.Linq.Expressions.Expression.Lambda(orCondition, parameter);
        }

        // --- OTOMATİK VERİ DOLDURMA (GÜVENLİK SİGORTASI) ---
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<IMultiTenant>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        // Yeni bir veri eklendiğinde kullanıcının TenantId'sini arkada gizlice biz basıyoruz
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
