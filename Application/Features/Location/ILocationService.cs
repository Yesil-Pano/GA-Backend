using GA.Application.Features.Location.DTOs;

namespace GA.Application.Features.Location
{
    public interface ILocationService
    {
        Task UpdateLocationAsync(Guid userId, double latitude, double longitude);
        Task<List<TeamMemberLocationDto>> GetTeamLocationsAsync(Guid tenantId);
    }
}
