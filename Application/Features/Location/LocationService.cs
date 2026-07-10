using GA.Application.Features.Location.DTOs;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GA.Application.Features.Location
{
    public class LocationService : ILocationService
    {
        private readonly ApplicationDbContext _context;

        public LocationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task UpdateLocationAsync(Guid userId, double latitude, double longitude)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new Exception("Kullanıcı bulunamadı.");

            // Point(x=longitude, y=latitude) — NetTopologySuite convention
            user.Location = new Point(longitude, latitude) { SRID = 4326 };
            user.LocationUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<List<TeamMemberLocationDto>> GetTeamLocationsAsync(Guid tenantId)
        {
            var isSuperAdmin = tenantId == Guid.Empty;
            var yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
            var trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

            return await _context.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted
                         && u.IsActive
                         && u.FieldWorkerProfile != null
                         && u.Location != null
                         && (isSuperAdmin
                             || u.TenantId == tenantId
                             || (tenantId == trugoTenantId && u.TenantId == yesilPanoTenantId)
                             || (tenantId == yesilPanoTenantId && u.TenantId == trugoTenantId)))
                .Select(u => new TeamMemberLocationDto
                {
                    UserId     = u.Id,
                    FullName   = u.FullName,
                    Username   = u.Username,
                    Latitude   = u.Location!.Y,
                    Longitude  = u.Location!.X,
                    UpdatedAt  = u.LocationUpdatedAt,
                })
                .ToListAsync();
        }
    }
}
