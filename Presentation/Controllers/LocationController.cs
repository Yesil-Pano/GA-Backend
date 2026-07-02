using GA.Application.Features.Location;
using GA.Application.Features.Location.DTOs;
using GA.Core.Interfaces;
using GA.Infrastructure.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHubContext<LocationHub> _hubContext;

        public LocationController(
            ILocationService locationService,
            ICurrentUserService currentUserService,
            IHubContext<LocationHub> hubContext)
        {
            _locationService = locationService;
            _currentUserService = currentUserService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Mobil uygulamanın kendi konumunu günceller ve takım üyelerine yayınlar.
        /// PUT /api/locations/me
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyLocation([FromBody] UpdateLocationRequest request)
        {
            var userId   = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            await _locationService.UpdateLocationAsync(userId, request.Latitude, request.Longitude);

            // SignalR ile aynı tenant'taki tüm bağlı web istemcilerine anlık bildir
            var payload = new
            {
                userId    = userId,
                latitude  = request.Latitude,
                longitude = request.Longitude,
                updatedAt = DateTime.UtcNow,
            };
            await _hubContext.Clients
                .Group($"tenant-{tenantId}")
                .SendAsync("LocationUpdated", payload);

            return NoContent();
        }

        /// <summary>
        /// Aynı tenant'taki konum paylaşmış tüm takım üyelerini döner.
        /// GET /api/locations/team
        /// </summary>
        [HttpGet("team")]
        public async Task<IActionResult> GetTeamLocations()
        {
            var tenantId = _currentUserService.TenantId;
            var locations = await _locationService.GetTeamLocationsAsync(tenantId);
            return Ok(locations);
        }
    }
}
