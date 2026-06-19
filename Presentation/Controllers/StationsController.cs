using GA.Application.Features.Auth.DTOs;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/stations
        [HttpGet]
        public IActionResult GetStations()
        {
            // Global Query Filter sayesinde giriş yapan kiracı dışındaki kimsenin verisi sızmaz!
            var stations = _context.Stations
                .Where(s => !s.IsDeleted)
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
                    position = new[] { s.Location.Y, s.Location.X } // [Lat, Lng]
                }).ToList();

            return Ok(stations);
        }

        // POST: api/stations
        [HttpPost]
        public IActionResult CreateStation([FromBody] CreateStationDto dto)
        {
            var station = new Station
            {
                Name = dto.Name,
                StatusType = dto.StatusType,
                PowerType = dto.PowerType,
                PersonnelName = dto.PersonnelName,
                PersonnelPhone = dto.PersonnelPhone,
                Edas = dto.Edas,
                Address = dto.Address,
                PointType = dto.PointType,
                City = dto.City,
                GeneralFilePath = dto.GeneralFilePath ?? "simulated_path.pdf",
                YgTescilBelgesiPath = dto.YgTescilBelgesiPath ?? "simulated_tg.pdf",
                YgSozlesmesiPath = dto.YgSozlesmesiPath ?? "simulated_sz.pdf",
                SabitFotograflarPath = dto.SabitFotograflarPath ?? "simulated_img.jpg",
                YillikBakimFormuPath = dto.YillikBakimFormuPath ?? "simulated_bk.pdf",
                YgIsletmeBelgesiPath = dto.YgIsletmeBelgesiPath ?? "simulated_ib.pdf",
                Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 }
            };

            _context.Stations.Add(station);
            _context.SaveChanges(); // Kiracı kimliği SaveChanges override ile otomatik basılır.

            return Ok(new { message = "Saha noktası başarıyla oluşturuldu!" });
        }
    }
}