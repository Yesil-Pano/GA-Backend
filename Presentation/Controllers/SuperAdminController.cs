using GA.Application.Features.Users;
using GA.Application.Features.Users.DTOs;
using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SuperAdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMemoryCache _cache;
        private readonly IUserManagementService _userManagementService;

        public SuperAdminController(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IMemoryCache cache,
            IUserManagementService userManagementService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _cache = cache;
            _userManagementService = userManagementService;
        }

        private async Task<bool> IsUserSuperAdmin()
        {
            var userId = _currentUserService.UserId;
            if (userId == Guid.Empty) return false;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user != null && user.Email == "admin@theobuz.com";
        }

        private void InvalidateTenantAccessCache(Guid tenantId)
            => _cache.Remove($"tenant-access:{tenantId:D}");

        [HttpPost("tenants")]
        public async Task<IActionResult> CreateTenant([FromBody] AdminCreateTenantDto dto)
        {
            if (!await IsUserSuperAdmin())
                return StatusCode(403, new { Message = "YETKİSİZ İŞLEM: Bu işlem sadece sistem yöneticisine özeldir." });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Firma adı zorunludur." });

            var isDemo = dto.IsDemo;
            DateTime? demoExpiresAt = null;
            if (isDemo)
            {
                var days = DemoDurations.ToDays(dto.DemoDuration);
                if (days == null)
                    return BadRequest(new { message = "Demo süre seçiniz: OneWeek, FifteenDays veya OneMonth." });
                demoExpiresAt = DateTime.UtcNow.AddDays(days.Value);
            }

            var tenant = new Tenant
            {
                Name = dto.Name.Trim(),
                TaxNumber = dto.TaxNumber?.Trim(),
                IsActive = true,
                IsDemo = isDemo,
                DemoExpiresAt = demoExpiresAt,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isDemo
                    ? $"{tenant.Name} DEMO firması oluşturuldu. Bitiş: {demoExpiresAt:yyyy-MM-dd HH:mm} UTC"
                    : $"{tenant.Name} firması başarıyla sisteme kaydedildi!",
                tenantId = tenant.Id,
                isDemo = tenant.IsDemo,
                demoExpiresAt = tenant.DemoExpiresAt?.ToString("yyyy-MM-dd HH:mm"),
            });
        }

        [HttpGet("tenants")]
        public async Task<IActionResult> GetAllTenants()
        {
            if (!await IsUserSuperAdmin()) return Forbid();

            var now = DateTime.UtcNow;
            var tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    taxNumber = t.TaxNumber,
                    isActive = t.IsActive,
                    isDemo = t.IsDemo,
                    demoExpiresAt = t.DemoExpiresAt.HasValue
                        ? t.DemoExpiresAt.Value.ToString("yyyy-MM-dd HH:mm")
                        : null,
                    isDemoExpired = t.IsDemo
                        && t.DemoExpiresAt.HasValue
                        && t.DemoExpiresAt.Value <= now,
                    createdAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                })
                .ToListAsync();

            return Ok(tenants);
        }

        /// <summary>DEMO süresini kaldırır (satın alma sonrası kalıcı firma).</summary>
        [HttpPost("tenants/{id:guid}/clear-demo")]
        public async Task<IActionResult> ClearDemo(Guid id)
        {
            if (!await IsUserSuperAdmin())
                return StatusCode(403, new { message = "Sadece Super Admin." });

            var tenant = await _context.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            if (tenant == null) return NotFound(new { message = "Firma bulunamadı." });

            tenant.IsDemo = false;
            tenant.DemoExpiresAt = null;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            InvalidateTenantAccessCache(id);

            return Ok(new { message = $"{tenant.Name}: DEMO süresi kaldırıldı. Erişim kalıcı açıldı.", isDemo = false });
        }

        /// <summary>DEMO süresini uzatır (aktif bitişten veya şimdiden + süre).</summary>
        [HttpPost("tenants/{id:guid}/extend-demo")]
        public async Task<IActionResult> ExtendDemo(Guid id, [FromBody] AdminExtendDemoDto dto)
        {
            if (!await IsUserSuperAdmin())
                return StatusCode(403, new { message = "Sadece Super Admin." });

            var days = DemoDurations.ToDays(dto.DemoDuration);
            if (days == null)
                return BadRequest(new { message = "Demo süre seçiniz: OneWeek, FifteenDays veya OneMonth." });

            var tenant = await _context.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
            if (tenant == null) return NotFound(new { message = "Firma bulunamadı." });

            var baseTime = (tenant.IsDemo && tenant.DemoExpiresAt.HasValue && tenant.DemoExpiresAt.Value > DateTime.UtcNow)
                ? tenant.DemoExpiresAt.Value
                : DateTime.UtcNow;

            tenant.IsDemo = true;
            tenant.DemoExpiresAt = baseTime.AddDays(days.Value);
            tenant.IsActive = true;
            tenant.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            InvalidateTenantAccessCache(id);

            return Ok(new
            {
                message = $"{tenant.Name}: DEMO süresi {DemoDurations.Label(days.Value)} uzatıldı.",
                isDemo = true,
                demoExpiresAt = tenant.DemoExpiresAt?.ToString("yyyy-MM-dd HH:mm"),
            });
        }

        [HttpPost("projects")]
        public async Task<IActionResult> CreateProject([FromBody] AdminCreateProjectDto dto)
        {
            if (!await IsUserSuperAdmin()) return Forbid();

            var project = new Project
            {
                Name = dto.Name,
                TenantId = dto.TenantId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"'{project.Name}' projesi ilgili firmaya başarıyla eklendi!" });
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserDto dto)
        {
            if (!await IsUserSuperAdmin()) return Forbid();

            try
            {
                var created = await _userManagementService.CreateUserAsync(new CreateManagedUserDto
                {
                    Username = dto.Username,
                    Email = dto.Email,
                    Password = dto.Password,
                    FullName = dto.FullName,
                    PhoneNumber = dto.PhoneNumber,
                    TenantId = dto.TenantId,
                    IsActive = true,
                    RoleNames = new List<string> { RoleNames.FieldWorker },
                });
                return Ok(new { Message = $"'{created.FullName}' kullanıcısı ilgili firmaya başarıyla eklendi!" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }

    public class AdminCreateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
        public bool IsDemo { get; set; }
        public string? DemoDuration { get; set; }
    }

    public class AdminExtendDemoDto
    {
        public string DemoDuration { get; set; } = string.Empty;
    }

    public class AdminCreateProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
    }

    public class AdminCreateUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
    }
}
