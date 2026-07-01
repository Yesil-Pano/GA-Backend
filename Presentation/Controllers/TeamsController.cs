using GA.Application.Features.Auth.DTOs;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TeamsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public TeamsController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTeams()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var teams = await _context.Users
                .Include(u => u.FieldWorkerProfile)
                    .ThenInclude(f => f!.Projects)
                .Where(u => (isSuperAdmin || u.TenantId == tenantId) && u.FieldWorkerProfile != null && !u.IsDeleted)
                .Select(u => new {
                    id = u.Id,
                    name = u.FullName,
                    username = u.Username,
                    email = u.Email,
                    phone = u.PhoneNumber,
                    project = u.FieldWorkerProfile!.Projects.Any()
                        ? string.Join(", ", u.FieldWorkerProfile.Projects.Select(p => p.Name))
                        : (u.FieldWorkerProfile.ProjectName ?? "-"),
                    projectIds = u.FieldWorkerProfile.Projects.Select(p => p.Id).ToList(),
                    plate = u.FieldWorkerProfile!.VehiclePlate ?? "-",
                    teamLeader = u.FieldWorkerProfile!.TeamLeader ?? "-",
                    position = u.FieldWorkerProfile!.HomeLocation != null
                        ? new[] { u.FieldWorkerProfile.HomeLocation.Y, u.FieldWorkerProfile.HomeLocation.X }
                        : new[] { 39.92077, 32.85411 }
                }).ToListAsync();

            return Ok(teams);
        }

        [HttpGet("lookups")]
        public async Task<IActionResult> GetTeamsLookups()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var projects = await _context.Projects
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted && (isSuperAdmin || p.TenantId == tenantId))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            return Ok(projects);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeam([FromBody] CreateTeamDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            Guid targetTenantId = tenantId;
            if (isSuperAdmin)
            {
                if (!dto.TenantId.HasValue || dto.TenantId == Guid.Empty)
                    return BadRequest(new { Message = "Super Admin olarak bir hedef firma seçmek zorundasınız!" });

                targetTenantId = dto.TenantId.Value;
            }

            var exists = await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == dto.Email || u.Username == dto.Username);
            if (exists) return BadRequest(new { Message = "Bu e-posta adresi veya kullanıcı adı zaten sistemde kayıtlı!" });

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.Name,
                PhoneNumber = dto.Phone,
                IsActive = true,
                TenantId = targetTenantId
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var profile = new FieldWorkerProfile
            {
                UserId = user.Id,
                ProjectName = dto.Project,
                VehiclePlate = dto.Plate,
                TeamLeader = dto.TeamLeader,
                // 🚀 HARİTA ÇÖZÜMÜ: Formdan gelen dinamik koordinatlar mühürleniyor (X: Lng, Y: Lat)
                HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 }
            };

            if (dto.ProjectIds != null && dto.ProjectIds.Any())
            {
                var selectedProjects = await _context.Projects
                    .IgnoreQueryFilters()
                    .Where(p => dto.ProjectIds.Contains(p.Id))
                    .ToListAsync();

                foreach (var project in selectedProjects)
                {
                    profile.Projects.Add(project);
                }
            }

            _context.FieldWorkerProfiles.Add(profile);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ekip başarıyla oluşturuldu!" });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTeam(Guid id, [FromBody] UpdateTeamDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                    .ThenInclude(f => f!.Projects)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted && (isSuperAdmin || u.TenantId == tenantId));

            if (user == null)
                return NotFound(new { Message = "Güncellenmek istenen ekip üyesi bulunamadı veya yetkiniz yetersiz." });

            var exists = await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id != id && (u.Email == dto.Email || u.Username == dto.Username));
            if (exists) return BadRequest(new { Message = "Bu e-posta veya kullanıcı adı başka bir personele aittir!" });

            user.FullName = dto.Name;
            user.PhoneNumber = dto.Phone;
            user.Username = dto.Username;
            user.Email = dto.Email;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            user.UpdatedAt = DateTime.UtcNow;

            if (user.FieldWorkerProfile != null)
            {
                user.FieldWorkerProfile.VehiclePlate = dto.Plate;
                user.FieldWorkerProfile.TeamLeader = dto.TeamLeader;
                // 🚀 HARİTA ÇÖZÜMÜ: Düzenleme ekranından gelen konumları güncelliyoruz
                user.FieldWorkerProfile.HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
                user.FieldWorkerProfile.UpdatedAt = DateTime.UtcNow;

                user.FieldWorkerProfile.Projects.Clear();
                if (dto.ProjectIds != null && dto.ProjectIds.Any())
                {
                    var selectedProjects = await _context.Projects
                        .IgnoreQueryFilters()
                        .Where(p => dto.ProjectIds.Contains(p.Id))
                        .ToListAsync();

                    foreach (var project in selectedProjects)
                    {
                        user.FieldWorkerProfile.Projects.Add(project);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Ekip bilgileri kurumsal standartlarda başarıyla güncellendi!" });
        }

        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateTeamLocationDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == Guid.Empty) return Unauthorized();

            var profile = await _context.FieldWorkerProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == dto.TeamUserId && p.User.TenantId == tenantId);

            if (profile == null) return NotFound(new { message = "Saha personeli profili bulunamadı." });

            profile.HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
            await _context.SaveChangesAsync();
            return Ok(new { message = "Saha konumu merkeze başarıyla raporlandı." });
        }
    }
}