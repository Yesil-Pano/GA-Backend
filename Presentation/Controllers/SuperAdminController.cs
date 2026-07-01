using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public SuperAdminController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
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

        [HttpPost("tenants")]
        public async Task<IActionResult> CreateTenant([FromBody] AdminCreateTenantDto dto)
        {
            if (!await IsUserSuperAdmin())
                return StatusCode(403, new { Message = "YETKİSİZ İŞLEM: Bu işlem sadece sistem yöneticisine özeldir." });

            var tenant = new Tenant
            {
                Name = dto.Name,
                TaxNumber = dto.TaxNumber,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"{tenant.Name} firması başarıyla sisteme kaydedildi!", TenantId = tenant.Id });
        }

        [HttpGet("tenants")]
        public async Task<IActionResult> GetAllTenants()
        {
            if (!await IsUserSuperAdmin()) return Forbid();

            var tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => !t.IsDeleted)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            return Ok(tenants);
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

            var emailExists = await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == dto.Email);
            if (emailExists) return BadRequest(new { Message = "Bu e-posta adresi sistemde zaten kayıtlı!" });

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                IsActive = true,
                TenantId = dto.TenantId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 🚀 ÇÖZÜM BURASI: Kullanıcının "Ekipler" (Teams) sayfasında görünmesi için 
            // ona varsayılan bir Saha Profili (FieldWorkerProfile) atıyoruz!
            var profile = new FieldWorkerProfile
            {
                UserId = user.Id,
                ProjectName = "-",
                VehiclePlate = "-",
                TeamLeader = "-",
                HomeLocation = new NetTopologySuite.Geometries.Point(32.85411, 39.92077) { SRID = 4326 }
            };

            _context.FieldWorkerProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"'{user.FullName}' kullanıcısı ilgili firmaya başarıyla eklendi!" });
        }
    }

    public class AdminCreateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string TaxNumber { get; set; } = string.Empty;
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