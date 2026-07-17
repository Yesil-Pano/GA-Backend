using GA.Application.Features.Partners;
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

        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        public TeamsController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTeams([FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var teams = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                    .ThenInclude(f => f!.Projects)
                .Where(u => !u.IsDeleted && u.FieldWorkerProfile != null &&
                            (isSuperAdmin ||
                             u.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && u.TenantId == _yesilPanoTenantId)))
                .Select(u => new {
                    id = u.Id,
                    name = u.FullName,
                    username = u.Username,
                    email = u.Email,
                    phone = u.PhoneNumber,
                    tenantId = u.TenantId,
                    project = u.FieldWorkerProfile!.Projects.Any()
                        ? string.Join(", ", u.FieldWorkerProfile.Projects.Select(p => p.Name))
                        : (u.FieldWorkerProfile.ProjectName ?? "-"),
                    projectNames = u.FieldWorkerProfile.Projects.Any()
                        ? u.FieldWorkerProfile.Projects.Select(p => p.Name).ToList()
                        : (string.IsNullOrWhiteSpace(u.FieldWorkerProfile.ProjectName)
                            ? new List<string>()
                            : new List<string> { u.FieldWorkerProfile.ProjectName }),
                    projectIds = u.FieldWorkerProfile.Projects.Select(p => p.Id).ToList(),
                    plate = u.FieldWorkerProfile!.VehiclePlate ?? "-",
                    teamLeader = u.FieldWorkerProfile!.TeamLeader ?? "-",
                    address = u.FieldWorkerProfile!.Address ?? "-",
                    city = u.FieldWorkerProfile!.City ?? "-",
                    district = u.FieldWorkerProfile!.District ?? "-",
                    hasLiveLocation = u.Location != null,
                    locationUpdatedAt = u.LocationUpdatedAt,
                    position = u.Location != null
                        ? new[] { u.Location.Y, u.Location.X }
                        : (u.FieldWorkerProfile!.HomeLocation != null
                            ? new[] { u.FieldWorkerProfile.HomeLocation.Y, u.FieldWorkerProfile.HomeLocation.X }
                            : new[] { 39.92077, 32.85411 })
                }).ToListAsync();

            if (isSuperAdmin)
            {
                var partner = PartnerCatalog.ResolveFilter(partnerKey);
                if (partner != null)
                {
                    teams = teams
                        .Where(t => PartnerCatalog.MatchesTeam(partner, t.tenantId, t.projectNames))
                        .ToList();
                }
            }

            // FE'ye projectNames alanını sızdırmadan aynısını dön (tenantId harita renkleri için)
            var payload = teams.Select(t => new {
                t.id,
                t.name,
                t.username,
                t.email,
                t.phone,
                t.tenantId,
                t.project,
                t.projectIds,
                t.plate,
                t.teamLeader,
                t.address,
                t.city,
                t.district,
                t.hasLiveLocation,
                t.locationUpdatedAt,
                t.position,
            });

            return Ok(payload);
        }

        [HttpGet("lookups")]
        public async Task<IActionResult> GetTeamsLookups([FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var projects = await _context.Projects
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted &&
                            (isSuperAdmin ||
                             p.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && p.TenantId == _yesilPanoTenantId)))
                .Select(p => new { id = p.Id, name = p.Name, tenantId = p.TenantId })
                .ToListAsync();

            if (isSuperAdmin)
            {
                var partner = PartnerCatalog.ResolveFilter(partnerKey);
                if (partner != null)
                {
                    projects = projects
                        .Where(p => PartnerCatalog.Matches(partner, p.tenantId, null, p.name))
                        .ToList();
                }
            }

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
                // 🚀 YENİ ALANLAR KAYDEDİLİYOR
                Address = dto.Address,
                City = dto.City,
                District = dto.District,
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
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted &&
                                          (isSuperAdmin ||
                                           u.TenantId == tenantId ||
                                           (tenantId == _trugoTenantId && u.TenantId == _yesilPanoTenantId)));

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
                // 🚀 YENİ ALANLAR GÜNCELLENİYOR
                user.FieldWorkerProfile.Address = dto.Address;
                user.FieldWorkerProfile.City = dto.City;
                user.FieldWorkerProfile.District = dto.District;

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

        /// <summary>
        /// Ekibi soft-delete eder; açık iş emirlerini Atanmamış'a çeker.
        /// DELETE /api/teams/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(Guid id)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted &&
                                          (isSuperAdmin ||
                                           u.TenantId == tenantId ||
                                           (tenantId == _trugoTenantId && u.TenantId == _yesilPanoTenantId)));

            if (user == null)
                return NotFound(new { Message = "Silinecek ekip bulunamadı veya yetkiniz yetersiz." });

            user.IsDeleted = true;
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.FieldWorkerProfile != null)
            {
                user.FieldWorkerProfile.IsDeleted = true;
                user.FieldWorkerProfile.UpdatedAt = DateTime.UtcNow;
            }

            var openStatuses = new[] { "Bekliyor", "Devam Ediyor" };
            var openOrders = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted
                            && w.AssignedToUserId == id
                            && openStatuses.Contains(w.Status))
                .ToListAsync();

            foreach (var order in openOrders)
            {
                order.AssignedToUserId = null;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "Ekip silindi. Açık iş emirleri Atanmamış durumuna alındı.",
                unassignedWorkOrderCount = openOrders.Count,
            });
        }

        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateTeamLocationDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == Guid.Empty) return Unauthorized();

            var profile = await _context.FieldWorkerProfiles
                .IgnoreQueryFilters()
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == dto.TeamUserId &&
                                          (p.User.TenantId == tenantId ||
                                           (tenantId == _trugoTenantId && p.User.TenantId == _yesilPanoTenantId)));

            if (profile == null) return NotFound(new { message = "Saha personeli profili bulunamadı." });

            profile.HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
            await _context.SaveChangesAsync();
            return Ok(new { message = "Saha konumu merkeze başarıyla raporlandı." });
        }
    }

    public class CreateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string TeamLeader { get; set; } = string.Empty;

        // 🚀 DTO'YA YENİ ALANLAR EKLENDİ
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;

        public List<Guid> ProjectIds { get; set; } = new List<Guid>();
        public Guid? TenantId { get; set; }
        public double Latitude { get; set; } = 39.92077;
        public double Longitude { get; set; } = 32.85411;
    }

    public class UpdateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string TeamLeader { get; set; } = string.Empty;

        // 🚀 DTO'YA YENİ ALANLAR EKLENDİ
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;

        public List<Guid> ProjectIds { get; set; } = new List<Guid>();
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class UpdateTeamLocationDto
    {
        public Guid TeamUserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}