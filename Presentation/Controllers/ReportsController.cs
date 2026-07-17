using System.Globalization;
using ClosedXML.Excel;
using GA.Application.Features.Common;
using GA.Application.Features.Partners;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    [Route("api/reports")]
    [ApiController]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        public ReportsController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// İş emri dinamik raporu (filtreli liste).
        /// GET /api/reports/work-orders
        /// </summary>
        [HttpGet("work-orders")]
        public async Task<IActionResult> GetWorkOrderReport(
            [FromQuery] string? category,
            [FromQuery] string? priority,
            [FromQuery] string? completionType,
            [FromQuery] Guid? openedByUserId,
            [FromQuery] Guid? assignedToUserId,
            [FromQuery] Guid? cityId,
            [FromQuery] Guid? districtId,
            [FromQuery] DateTime? startFrom,
            [FromQuery] DateTime? startTo,
            [FromQuery] string? search,
            [FromQuery] string? partnerKey,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var query = BuildReportQuery(
                category, priority, completionType,
                openedByUserId, assignedToUserId,
                cityId, districtId, startFrom, startTo, search, partnerKey);

            var total = await query.CountAsync();

            var rawItems = await query
                .OrderByDescending(w => w.StartDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(w => new
                {
                    id = w.Id,
                    title = w.Title,
                    customerName = w.CustomerName,
                    category = w.WorkCategory,
                    type = w.WorkType,
                    priority = w.Priority,
                    status = w.Status,
                    description = w.Description,
                    mobileDescription = w.MobileDescription,
                    address = w.Address,
                    startDate = w.StartDate,
                    endDate = w.EndDate,
                    startedAt = w.StartedAt,
                    completedAt = w.CompletedAt,
                    cancelledAt = w.CancelledAt,
                    openedByUserId = w.OpenedByUserId,
                    openedByUserName = _context.Users.Where(u => u.Id == w.OpenedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "-",
                    assignedToUserId = w.AssignedToUserId,
                    assignedToUserName = w.AssignedToUserId == null
                        ? "Atanmamış"
                        : (_context.Users.Where(u => u.Id == w.AssignedToUserId).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış"),
                    cityId = w.CityId,
                    districtId = w.DistrictId,
                    cityName = w.CityRef != null
                        ? w.CityRef.Name
                        : _context.Stations
                            .Where(s => !s.IsDeleted && s.Name.ToLower() == w.CustomerName.ToLower())
                            .Select(s => s.City)
                            .FirstOrDefault(),
                    districtName = w.DistrictRef != null
                        ? w.DistrictRef.Name
                        : _context.Stations
                            .Where(s => !s.IsDeleted && s.Name.ToLower() == w.CustomerName.ToLower())
                            .Select(s => s.District)
                            .FirstOrDefault(),
                    assignmentLabel = w.AssignedToUserId == null ? "Atanmamış" : "Atanmış",
                })
                .ToListAsync();

            var items = rawItems.Select(w => new
            {
                w.id,
                w.title,
                w.customerName,
                w.category,
                w.type,
                w.priority,
                w.status,
                w.description,
                w.mobileDescription,
                w.address,
                startDate = TurkeyTime.Format(w.startDate),
                endDate = TurkeyTime.Format(w.endDate),
                startedAt = TurkeyTime.Format(w.startedAt),
                completedAt = TurkeyTime.Format(w.completedAt),
                cancelledAt = TurkeyTime.Format(w.cancelledAt),
                durationMinutes = TurkeyTime.DurationMinutes(w.startedAt, w.completedAt),
                w.openedByUserId,
                w.openedByUserName,
                w.assignedToUserId,
                w.assignedToUserName,
                w.cityId,
                w.districtId,
                w.cityName,
                w.districtName,
                w.assignmentLabel,
            }).ToList();

            return Ok(new
            {
                total,
                page,
                pageSize,
                items,
            });
        }

        /// <summary>
        /// Aynı filtrelerle gerçek Excel (.xlsx) indirme.
        /// GET /api/reports/work-orders/export
        /// </summary>
        [HttpGet("work-orders/export")]
        public async Task<IActionResult> ExportWorkOrderReport(
            [FromQuery] string? category,
            [FromQuery] string? priority,
            [FromQuery] string? completionType,
            [FromQuery] Guid? openedByUserId,
            [FromQuery] Guid? assignedToUserId,
            [FromQuery] Guid? cityId,
            [FromQuery] Guid? districtId,
            [FromQuery] DateTime? startFrom,
            [FromQuery] DateTime? startTo,
            [FromQuery] string? search,
            [FromQuery] string? partnerKey)
        {
            var query = BuildReportQuery(
                category, priority, completionType,
                openedByUserId, assignedToUserId,
                cityId, districtId, startFrom, startTo, search, partnerKey);

            var rows = await query
                .OrderByDescending(w => w.StartDate)
                .Take(20_000)
                .Select(w => new
                {
                    w.CustomerName,
                    w.Title,
                    w.WorkCategory,
                    w.WorkType,
                    w.Priority,
                    w.Status,
                    w.Description,
                    w.MobileDescription,
                    w.Address,
                    StartDate = w.StartDate,
                    EndDate = w.EndDate,
                    StartedAt = w.StartedAt,
                    CompletedAt = w.CompletedAt,
                    CancelledAt = w.CancelledAt,
                    OpenedBy = _context.Users.Where(u => u.Id == w.OpenedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "-",
                    AssignedTo = w.AssignedToUserId == null
                        ? "Atanmamış"
                        : (_context.Users.Where(u => u.Id == w.AssignedToUserId).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış"),
                    City = w.CityRef != null
                        ? w.CityRef.Name
                        : _context.Stations.Where(s => !s.IsDeleted && s.Name.ToLower() == w.CustomerName.ToLower()).Select(s => s.City).FirstOrDefault() ?? "",
                    District = w.DistrictRef != null
                        ? w.DistrictRef.Name
                        : _context.Stations.Where(s => !s.IsDeleted && s.Name.ToLower() == w.CustomerName.ToLower()).Select(s => s.District).FirstOrDefault() ?? "",
                    Assignment = w.AssignedToUserId == null ? "Atanmamış" : "Atanmış",
                })
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("İş Emirleri");

            string[] headers =
            [
                "Nokta", "Başlık", "İş Kategorisi", "İş Tipi", "Öncelik", "Durum",
                "Genel Açıklama", "Mühendis Açıklaması", "Adres",
                "Planlanan Başlangıç", "Planlanan Bitiş",
                "Gerçek Başlangıç", "Bitiş Tarihi", "İptal Tarihi", "Süre (dk)",
                "İşi Açan", "İş Atanan", "İl", "İlçe", "Atama"
            ];

            for (var c = 0; c < headers.Length; c++)
                sheet.Cell(1, c + 1).Value = headers[c];

            var headerRange = sheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E");
            headerRange.Style.Font.FontColor = XLColor.White;

            var rowIdx = 2;
            foreach (var r in rows)
            {
                var duration = TurkeyTime.DurationMinutes(r.StartedAt, r.CompletedAt);
                sheet.Cell(rowIdx, 1).Value = r.CustomerName;
                sheet.Cell(rowIdx, 2).Value = r.Title;
                sheet.Cell(rowIdx, 3).Value = r.WorkCategory;
                sheet.Cell(rowIdx, 4).Value = r.WorkType;
                sheet.Cell(rowIdx, 5).Value = r.Priority;
                sheet.Cell(rowIdx, 6).Value = r.Status;
                sheet.Cell(rowIdx, 7).Value = r.Description;
                sheet.Cell(rowIdx, 8).Value = r.MobileDescription;
                sheet.Cell(rowIdx, 9).Value = r.Address;
                sheet.Cell(rowIdx, 10).Value = TurkeyTime.Format(r.StartDate);
                sheet.Cell(rowIdx, 11).Value = TurkeyTime.Format(r.EndDate);
                sheet.Cell(rowIdx, 12).Value = TurkeyTime.Format(r.StartedAt);
                sheet.Cell(rowIdx, 13).Value = TurkeyTime.Format(r.CompletedAt);
                sheet.Cell(rowIdx, 14).Value = TurkeyTime.Format(r.CancelledAt);
                sheet.Cell(rowIdx, 15).Value = duration.HasValue ? duration.Value : "";
                sheet.Cell(rowIdx, 16).Value = r.OpenedBy;
                sheet.Cell(rowIdx, 17).Value = r.AssignedTo;
                sheet.Cell(rowIdx, 18).Value = r.City;
                sheet.Cell(rowIdx, 19).Value = r.District;
                sheet.Cell(rowIdx, 20).Value = r.Assignment;
                rowIdx++;
            }

            sheet.Columns().AdjustToContents(1, 60);
            sheet.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var bytes = stream.ToArray();
            var fileName = $"is-emri-raporu-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private IQueryable<Core.Domain.Entities.WorkOrder> BuildReportQuery(
            string? category,
            string? priority,
            string? completionType,
            Guid? openedByUserId,
            Guid? assignedToUserId,
            Guid? cityId,
            Guid? districtId,
            DateTime? startFrom,
            DateTime? startTo,
            string? search,
            string? partnerKey = null)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var query = _context.WorkOrders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => !w.IsDeleted &&
                            (isSuperAdmin ||
                             w.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && w.TenantId == _yesilPanoTenantId) ||
                             (tenantId == _yesilPanoTenantId && w.TenantId == _trugoTenantId)));

            if (isSuperAdmin)
            {
                var partner = PartnerCatalog.ResolveFilter(partnerKey);
                if (partner != null)
                {
                    var stationRows = _context.Stations
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(s => !s.IsDeleted)
                        .Select(s => new { s.Name, s.TenantId, s.OwnerCompany })
                        .ToList();

                    var partnerStationNames = stationRows
                        .Where(s => PartnerCatalog.Matches(partner, s.TenantId, s.OwnerCompany, s.Name))
                        .Select(s => s.Name.Trim().ToLowerInvariant())
                        .ToHashSet();

                    var candidateIds = query.Select(w => new { w.Id, w.CustomerName, w.TenantId }).ToList();
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

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(w => w.WorkCategory == category);

            if (!string.IsNullOrWhiteSpace(priority))
                query = query.Where(w => w.Priority == priority);

            if (!string.IsNullOrWhiteSpace(completionType))
            {
                var ct = completionType.Trim();
                if (ct.Equals("Atanmamış", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(w => w.AssignedToUserId == null);
                else if (ct.Equals("Atanmış", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(w => w.AssignedToUserId != null);
                else if (ct.Equals("İptal Edildi", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(w => w.Status == "İptal Edildi" || w.Status == "İptal");
                else
                    query = query.Where(w => w.Status == ct);
            }

            if (openedByUserId.HasValue && openedByUserId != Guid.Empty)
                query = query.Where(w => w.OpenedByUserId == openedByUserId);

            if (assignedToUserId.HasValue && assignedToUserId != Guid.Empty)
                query = query.Where(w => w.AssignedToUserId == assignedToUserId);

            if (cityId.HasValue && cityId != Guid.Empty)
            {
                var cityName = _context.Cities.Where(c => c.Id == cityId).Select(c => c.Name).FirstOrDefault();
                query = query.Where(w =>
                    w.CityId == cityId
                    || (w.CityId == null
                        && _context.Stations.Any(s =>
                            !s.IsDeleted
                            && s.Name.ToLower() == w.CustomerName.ToLower()
                            && (s.CityId == cityId
                                || (cityName != null && s.City.ToLower() == cityName.ToLower())))));
            }

            if (districtId.HasValue && districtId != Guid.Empty)
            {
                var districtName = _context.Districts.Where(d => d.Id == districtId).Select(d => d.Name).FirstOrDefault();
                query = query.Where(w =>
                    w.DistrictId == districtId
                    || (w.DistrictId == null
                        && _context.Stations.Any(s =>
                            !s.IsDeleted
                            && s.Name.ToLower() == w.CustomerName.ToLower()
                            && (s.DistrictId == districtId
                                || (districtName != null && s.District != null && s.District.ToLower() == districtName.ToLower())))));
            }

            if (startFrom.HasValue)
            {
                var from = DateTime.SpecifyKind(startFrom.Value, DateTimeKind.Utc);
                query = query.Where(w => w.StartDate >= from);
            }

            if (startTo.HasValue)
            {
                var to = DateTime.SpecifyKind(startTo.Value, DateTimeKind.Utc);
                query = query.Where(w => w.StartDate <= to);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                query = query.Where(w =>
                    w.CustomerName.ToLower().Contains(q)
                    || w.Title.ToLower().Contains(q)
                    || w.Description.ToLower().Contains(q)
                    || w.MobileDescription.ToLower().Contains(q)
                    || w.Address.ToLower().Contains(q));
            }

            return query;
        }
    }
}
