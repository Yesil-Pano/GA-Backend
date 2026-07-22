using GA.Application.Features.Auth.DTOs;
using GA.Application.Features.Geo;
using GA.Application.Features.Partners;
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

        // 🚀 B2B FİRMA KİMLİKLERİ
        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        public StationsController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetStations(
            [FromQuery] Guid? projectId,
            [FromQuery] Guid? tenantIdFilter,
            [FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            // Firma kullanıcıları yalnızca kendi tenant istasyonlarını görür.
            var query = _context.Stations
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted &&
                            (isSuperAdmin || s.TenantId == tenantId));

            string? projectName = null;
            Guid? projectTenantId = null;

            if (projectId.HasValue && projectId.Value != Guid.Empty)
            {
                var project = await _context.Projects
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == projectId.Value && !p.IsDeleted);

                if (project != null)
                {
                    projectName = project.Name;
                    projectTenantId = project.TenantId;
                }
            }

            if (tenantIdFilter.HasValue && tenantIdFilter.Value != Guid.Empty)
            {
                projectTenantId = tenantIdFilter.Value;
            }

            PartnerDefinition? partner = null;
            if (isSuperAdmin)
            {
                // "all" → filtre yok; aksi halde Matches ile (OwnerCompany öncelikli) süzülür.
                partner = PartnerCatalog.ResolveFilter(partnerKey);
            }
            else if (projectTenantId.HasValue)
            {
                query = query.Where(s => s.TenantId == projectTenantId.Value);
            }

            var stations = await query
                .Select(s => new {
                    id = s.Id,
                    name = s.Name,
                    statusType = s.StatusType,
                    powerType = s.PowerType,
                    personnelName = s.PersonnelName,
                    personnelPhone = s.PersonnelPhone,
                    edas = s.Edas,
                    city = s.City,
                    district = s.District,
                    address = s.Address,
                    pointType = s.PointType,
                    ownerCompany = s.OwnerCompany,
                    tenantId = s.TenantId,
                    cityId = s.CityId,
                    districtId = s.DistrictId,
                    position = new[] { s.Location.Y, s.Location.X }
                }).ToListAsync();

            if (partner != null)
            {
                stations = stations.Where(s =>
                    PartnerCatalog.Matches(partner, s.tenantId, s.ownerCompany, s.name)).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(projectName))
            {
                var tokens = projectName
                    .Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length >= 3)
                    .Select(t => t.ToLowerInvariant())
                    .ToArray();

                if (tokens.Length > 0)
                {
                    var ownershipFiltered = stations.Where(s =>
                    {
                        var owner = (s.ownerCompany ?? "").ToLowerInvariant();
                        var name = (s.name ?? "").ToLowerInvariant();
                        return tokens.Any(token => owner.Contains(token) || name.Contains(token));
                    }).ToList();

                    if (ownershipFiltered.Count > 0)
                        stations = ownershipFiltered;
                }
            }

            return Ok(stations);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetStationDetails(Guid id)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var station = await _context.Stations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted &&
                                          (isSuperAdmin || s.TenantId == tenantId));

            if (station == null) return NotFound();

            return Ok(new
            {
                id = station.Id,
                name = station.Name,
                statusType = station.StatusType,
                powerType = station.PowerType,
                personnelName = station.PersonnelName,
                personnelPhone = station.PersonnelPhone,
                edas = station.Edas,
                address = station.Address,
                pointType = station.PointType,
                city = station.City,
                chargepointId = station.ChargepointId,
                deviceVendor = station.DeviceVendor,
                vendorModel = station.VendorModel,
                socketCount = station.SocketCount,
                devicePower = station.DevicePower,
                district = station.District,
                partnerStatus = station.PartnerStatus,
                ownerCompany = station.OwnerCompany,
                estimatedDate = station.EstimatedDate,
                position = new[] { station.Location.Y, station.Location.X }
            });
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

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateStation(Guid id, [FromBody] UpdateStationDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var station = await _context.Stations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted &&
                                          (isSuperAdmin || s.TenantId == tenantId));

            if (station == null) return NotFound(new { message = "İstasyon bulunamadı." });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "İstasyon adı zorunludur." });

            if (dto.Latitude is < 35 or > 43 || dto.Longitude is < 25 or > 45)
                return BadRequest(new { message = "Koordinatlar Türkiye sınırları dışında görünüyor." });

            station.Name = dto.Name.Trim();
            station.StatusType = string.IsNullOrWhiteSpace(dto.StatusType) ? station.StatusType : dto.StatusType.Trim();
            station.PowerType = string.IsNullOrWhiteSpace(dto.PowerType) ? "-" : dto.PowerType.Trim();
            station.PersonnelName = string.IsNullOrWhiteSpace(dto.PersonnelName) ? "-" : dto.PersonnelName.Trim();
            station.PersonnelPhone = string.IsNullOrWhiteSpace(dto.PersonnelPhone) ? "-" : dto.PersonnelPhone.Trim();
            station.Edas = string.IsNullOrWhiteSpace(dto.Edas) ? "-" : dto.Edas.Trim();
            station.Address = string.IsNullOrWhiteSpace(dto.Address) ? "-" : dto.Address.Trim();
            station.PointType = string.IsNullOrWhiteSpace(dto.PointType) ? station.PointType : dto.PointType.Trim();
            station.City = string.IsNullOrWhiteSpace(dto.City) ? station.City : dto.City.Trim();
            station.District = string.IsNullOrWhiteSpace(dto.District) ? null : dto.District.Trim();
            station.OwnerCompany = string.IsNullOrWhiteSpace(dto.OwnerCompany) ? station.OwnerCompany : dto.OwnerCompany.Trim();
            station.Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
            station.UpdatedAt = DateTime.UtcNow;

            var (resolvedCityId, resolvedDistrictId) = await GeoResolver.ResolveAsync(
                _context,
                null,
                null,
                station.City,
                station.District);

            station.CityId = resolvedCityId;
            station.DistrictId = resolvedDistrictId;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "İstasyon güncellendi.",
                id = station.Id,
                name = station.Name,
                statusType = station.StatusType,
                powerType = station.PowerType,
                personnelName = station.PersonnelName,
                personnelPhone = station.PersonnelPhone,
                edas = station.Edas,
                address = station.Address,
                pointType = station.PointType,
                city = station.City,
                district = station.District,
                ownerCompany = station.OwnerCompany,
                cityId = station.CityId,
                districtId = station.DistrictId,
                position = new[] { station.Location.Y, station.Location.X }
            });
        }
    }
}