using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    /// <summary>
    /// İl / ilçe referans API'si (raporlama ve form filtreleri için).
    /// </summary>
    [Route("api/geo")]
    [ApiController]
    [Authorize]
    public class GeoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GeoController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tüm illeri alfabetik olarak döner.
        /// GET /api/geo/cities
        /// </summary>
        [HttpGet("cities")]
        public async Task<IActionResult> GetCities()
        {
            var cities = await _context.Cities
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    latitude = c.Latitude,
                    longitude = c.Longitude,
                })
                .ToListAsync();

            return Ok(cities);
        }

        /// <summary>
        /// Seçilen ile bağlı ilçeleri döner.
        /// GET /api/geo/districts?cityId={guid}
        /// </summary>
        [HttpGet("districts")]
        public async Task<IActionResult> GetDistricts([FromQuery] Guid cityId)
        {
            if (cityId == Guid.Empty)
                return BadRequest(new { message = "cityId zorunludur." });

            var cityExists = await _context.Cities.AsNoTracking().AnyAsync(c => c.Id == cityId);
            if (!cityExists)
                return NotFound(new { message = "İl bulunamadı." });

            var districts = await _context.Districts
                .AsNoTracking()
                .Where(d => d.CityId == cityId)
                .OrderBy(d => d.Name)
                .Select(d => new
                {
                    id = d.Id,
                    name = d.Name,
                    cityId = d.CityId,
                    latitude = d.Latitude,
                    longitude = d.Longitude,
                })
                .ToListAsync();

            return Ok(districts);
        }
    }
}
