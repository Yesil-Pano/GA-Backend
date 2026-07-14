using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.Geo
{
    public static class GeoResolver
    {
        /// <summary>
        /// Şehir / ilçe adından (veya mevcut FK'den) Cities/Districts Id çözümler.
        /// </summary>
        public static async Task<(Guid? CityId, Guid? DistrictId)> ResolveAsync(
            ApplicationDbContext context,
            Guid? cityId,
            Guid? districtId,
            string? cityName,
            string? districtName)
        {
            Guid? resolvedCityId = cityId;
            Guid? resolvedDistrictId = districtId;

            if ((!resolvedCityId.HasValue || resolvedCityId == Guid.Empty) && !string.IsNullOrWhiteSpace(cityName))
            {
                var normalized = cityName.Trim();
                resolvedCityId = await context.Cities
                    .AsNoTracking()
                    .Where(c => c.Name.ToLower() == normalized.ToLower())
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync();
            }

            if (resolvedCityId == Guid.Empty)
                resolvedCityId = null;

            if ((!resolvedDistrictId.HasValue || resolvedDistrictId == Guid.Empty)
                && resolvedCityId.HasValue
                && !string.IsNullOrWhiteSpace(districtName))
            {
                var normalizedDistrict = districtName.Trim();
                resolvedDistrictId = await context.Districts
                    .AsNoTracking()
                    .Where(d => d.CityId == resolvedCityId
                                && d.Name.ToLower() == normalizedDistrict.ToLower())
                    .Select(d => (Guid?)d.Id)
                    .FirstOrDefaultAsync();
            }

            if (resolvedDistrictId == Guid.Empty)
                resolvedDistrictId = null;

            // İlçe verilmiş ama il yoksa ilçeden ile çık
            if ((!resolvedCityId.HasValue) && resolvedDistrictId.HasValue)
            {
                resolvedCityId = await context.Districts
                    .AsNoTracking()
                    .Where(d => d.Id == resolvedDistrictId)
                    .Select(d => (Guid?)d.CityId)
                    .FirstOrDefaultAsync();
            }

            return (resolvedCityId, resolvedDistrictId);
        }

        public static async Task<(Guid? CityId, Guid? DistrictId)> ResolveFromStationAsync(
            ApplicationDbContext context,
            Station station)
        {
            return await ResolveAsync(
                context,
                station.CityId,
                station.DistrictId,
                station.City,
                station.District);
        }
    }
}
