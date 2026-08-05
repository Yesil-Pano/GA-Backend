using GA.Application.Features.Common;
using GA.Application.Features.Partners;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPartnerTenantService _partnerTenantService;

        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        public DashboardController(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IPartnerTenantService partnerTenantService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _partnerTenantService = partnerTenantService;
        }

        /// <summary>GET /api/dashboard/summary</summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var query = _context.WorkOrders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => !w.IsDeleted && !w.IsPeriodic
                            && (isSuperAdmin ||
                                w.TenantId == tenantId ||
                                (tenantId == _yesilPanoTenantId && w.TenantId == _trugoTenantId)));

            if (isSuperAdmin)
            {
                var partner = await _partnerTenantService.ResolveFilterAsync(partnerKey);
                if (partner != null)
                {
                    var stationRows = await _context.Stations
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(s => !s.IsDeleted)
                        .Select(s => new { s.Name, s.TenantId, s.OwnerCompany })
                        .ToListAsync();

                    var partnerStationNames = stationRows
                        .Where(s => PartnerCatalog.Matches(partner, s.TenantId, s.OwnerCompany, s.Name))
                        .Select(s => s.Name.Trim().ToLowerInvariant())
                        .ToHashSet();

                    var candidateIds = await query.Select(w => new { w.Id, w.CustomerName, w.TenantId }).ToListAsync();
                    var allowedIds = candidateIds
                        .Where(w =>
                            (partner.TenantId.HasValue && w.TenantId == partner.TenantId.Value)
                            || partnerStationNames.Contains((w.CustomerName ?? string.Empty).Trim().ToLowerInvariant())
                            || PartnerCatalog.Matches(partner, w.TenantId, null, w.CustomerName))
                        .Select(w => w.Id)
                        .ToHashSet();

                    query = query.Where(w => allowedIds.Contains(w.Id));
                }
            }

            var all = await query
                .Select(w => new
                {
                    w.Priority,
                    w.Status,
                    w.WorkType,
                    w.CompletedAt,
                    w.CreatedAt,
                    w.StartDate,
                })
                .ToListAsync();

            var (todayStartUtc, todayEndUtc) = GetTodayUtcRange();
            var monthStartUtc = GetMonthStartUtc(DateTime.UtcNow);

            var completedToday = all.Count(w =>
                w.CompletedAt.HasValue &&
                w.CompletedAt.Value >= todayStartUtc &&
                w.CompletedAt.Value < todayEndUtc);

            var monthlyDays = Enumerable.Range(0, DateTime.DaysInMonth(
                    TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TurkeyTimeZone()).Year,
                    TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TurkeyTimeZone()).Month))
                .Select(offset =>
                {
                    var dayStart = monthStartUtc.AddDays(offset);
                    var dayEnd = dayStart.AddDays(1);
                    var created = all.Count(w => w.CreatedAt >= dayStart && w.CreatedAt < dayEnd);
                    var completed = all.Count(w =>
                        w.CompletedAt.HasValue &&
                        w.CompletedAt.Value >= dayStart &&
                        w.CompletedAt.Value < dayEnd);
                    var pct = created > 0 ? Math.Round(completed * 100.0 / created, 1) : 0.0;
                    return new { day = offset + 1, created, completed, completionPct = pct };
                })
                .ToList();

            var dailyFaults = Enumerable.Range(0, 14)
                .Select(i =>
                {
                    var dayStart = todayStartUtc.AddDays(-(13 - i));
                    var dayEnd = dayStart.AddDays(1);
                    var count = all.Count(w =>
                        string.Equals(w.WorkType, "Arıza", StringComparison.OrdinalIgnoreCase) &&
                        w.CreatedAt >= dayStart &&
                        w.CreatedAt < dayEnd);
                    return new
                    {
                        date = TurkeyTime.Format(dayStart, "yyyy-MM-dd"),
                        label = TurkeyTime.Format(dayStart, "dd MMM"),
                        count,
                    };
                })
                .ToList();

            var activeFieldWorkers = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(u => !u.IsDeleted && u.IsActive && u.FieldWorkerProfile != null &&
                                 (isSuperAdmin || u.TenantId == tenantId));

            return Ok(new
            {
                stats = new
                {
                    totalCount = all.Count,
                    acilCount = all.Count(w => w.Priority == "Acil"),
                    ortaCount = all.Count(w => w.Priority == "Orta"),
                    dusukCount = all.Count(w => w.Priority == "Düşük"),
                    devamEdiyorCount = all.Count(w => w.Status == "Devam Ediyor"),
                    bekliyorCount = all.Count(w => w.Status == "Bekliyor"),
                    tamamlandiCount = all.Count(w => w.Status == "Tamamlandı"),
                    iptalEdildiCount = all.Count(w =>
                        w.Status == "İptal Edildi" || w.Status == "İptal"),
                    completedToday,
                    activeUsers = activeFieldWorkers,
                },
                monthlyActivity = monthlyDays,
                dailyFaults,
                generatedAt = TurkeyTime.FormatApi(DateTime.UtcNow),
            });
        }

        private static TimeZoneInfo TurkeyTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("UTC+03", TimeSpan.FromHours(3), "UTC+03", "UTC+03");
                }
            }
        }

        private static (DateTime startUtc, DateTime endUtc) GetTodayUtcRange()
        {
            var tz = TurkeyTimeZone();
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var localStart = localNow.Date;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), tz);
            return (startUtc, startUtc.AddDays(1));
        }

        private static DateTime GetMonthStartUtc(DateTime utcNow)
        {
            var tz = TurkeyTimeZone();
            var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
            var monthStartLocal = new DateTime(local.Year, local.Month, 1);
            return TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(monthStartLocal, DateTimeKind.Unspecified), tz);
        }
    }
}
