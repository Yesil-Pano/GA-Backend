using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TeamsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/teams
        [HttpGet]
        public IActionResult GetTeams()
        {
            // 💡 Sadece giriş yapmış şirketin ekipleri otomatik süzülerek gelir!
            var teams = _context.Users
                .Include(u => u.FieldWorkerProfile)
                .Where(u => u.FieldWorkerProfile != null && !u.IsDeleted)
                .Select(u => new {
                    id = u.Id,
                    name = u.FullName,
                    phone = u.PhoneNumber,
                    project = u.FieldWorkerProfile!.ProjectName ?? "-",
                    plate = u.FieldWorkerProfile!.VehiclePlate ?? "-",
                    teamLeader = u.FieldWorkerProfile!.TeamLeader ?? "-",
                    position = u.FieldWorkerProfile!.HomeLocation != null
                        ? new[] { u.FieldWorkerProfile.HomeLocation.Y, u.FieldWorkerProfile.HomeLocation.X }
                        : new[] { 39.92077, 32.85411 } // Ankara default
                }).ToList();

            return Ok(teams);
        }

        // POST: api/teams
        [HttpPost]
        public IActionResult CreateTeam([FromBody] CreateTeamDto dto)
        {
            string safeName = dto.Name.ToLower().Replace(" ", ".");
            string generatedEmail = $"{safeName}_{Guid.NewGuid().ToString().Substring(0, 4)}@teamer.local";

            var user = new User
            {
                Username = $"{safeName}_{Guid.NewGuid().ToString().Substring(0, 4)}",
                Email = generatedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Teamer123!"), // Varsayılan kurumsal giriş şifresi
                FullName = dto.Name,
                PhoneNumber = dto.Phone,
                IsActive = true
            };

            _context.Users.Add(user);
            _context.SaveChanges(); // DB tetiklenir, SaveChangesAsync override kimliği basar.

            var profile = new FieldWorkerProfile
            {
                UserId = user.Id,
                ProjectName = dto.Project,
                VehiclePlate = dto.Plate,
                TeamLeader = dto.TeamLeader,
                HomeLocation = new NetTopologySuite.Geometries.Point(32.85411, 39.92077) { SRID = 4326 }
            };

            _context.FieldWorkerProfiles.Add(profile);
            _context.SaveChanges();

            return Ok(new { message = "Ekip başarıyla oluşturuldu!" });
        }

        // 🚀 REVIZYON: Web metodlarını bozmadan, Mobil uygulamanın 7/24 arka plandan tetikleyeceği konum ucu!
        [HttpPost("update-location")]
        public IActionResult UpdateLocation([FromBody] UpdateTeamLocationDto dto)
        {
            var profile = _context.FieldWorkerProfiles
                .FirstOrDefault(p => p.UserId == dto.TeamUserId);

            if (profile == null)
                return NotFound(new { message = "Saha personeli profili bulunamadı." });

            // 📍 TEKNİK UYUMLULUK FIX: Boylam (X) ve Enlem (Y) sırasıyla NetTopologySuite Point nesnesine sarılır.
            // GetTeams metodu HomeLocation.Y ve X okuduğu için mobil veri attıkça web haritası kendiliğinden canlanır!
            profile.HomeLocation = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };

            _context.SaveChanges();
            return Ok(new { message = "Saha konumu merkeze başarıyla raporlandı." });
        }
    }

    public class CreateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Plate { get; set; } = string.Empty;
        public string TeamLeader { get; set; } = string.Empty;
    }

    // 🚀 REVIZYON DTO: Mobil verileri hatasız taşımak için kurumsal veri kontratı
    public class UpdateTeamLocationDto
    {
        public Guid TeamUserId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}