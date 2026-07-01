using GA.Application.Features.Auth.DTOs;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public StationsController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetStations()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty; // 🚀 VIP KONTROLÜ

            var stations = await _context.Stations
                .Where(s => !s.IsDeleted && (isSuperAdmin || s.TenantId == tenantId))
                .Select(s => new {
                    id = s.Id,
                    name = s.Name,
                    statusType = s.StatusType,
                    powerType = s.PowerType,
                    personnelName = s.PersonnelName,
                    personnelPhone = s.PersonnelPhone,
                    edas = s.Edas,
                    address = s.Address,
                    pointType = s.PointType,
                    city = s.City,
                    generalFilePath = s.GeneralFilePath ?? "Yüklenmedi",
                    ygTescilBelgesiPath = s.YgTescilBelgesiPath ?? "Yüklenmedi",
                    ygSozlesmesiPath = s.YgSozlesmesiPath ?? "Yüklenmedi",
                    sabitFotograflarPath = s.SabitFotograflarPath ?? "Yüklenmedi",
                    yillikBakimFormuPath = s.YillikBakimFormuPath ?? "Yüklenmedi",
                    ygIsletmeBelgesiPath = s.YgIsletmeBelgesiPath ?? "Yüklenmedi",
                    position = new[] { s.Location.Y, s.Location.X }
                }).ToListAsync();

            return Ok(stations);
        }

        [HttpPost]
        public async Task<IActionResult> CreateStation([FromBody] CreateStationDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == Guid.Empty) return Unauthorized();

            var station = new Station { Name = dto.Name, StatusType = dto.StatusType, PowerType = dto.PowerType, PersonnelName = dto.PersonnelName, PersonnelPhone = dto.PersonnelPhone, Edas = dto.Edas, Address = dto.Address, PointType = dto.PointType, City = dto.City, Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 }, TenantId = tenantId };
            _context.Stations.Add(station);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Saha noktası başarıyla oluşturuldu!" });
        }
    }
}