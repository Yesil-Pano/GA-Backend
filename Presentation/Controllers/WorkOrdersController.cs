using GA.Application.Features.Geo;
using GA.Application.Features.Notifications;
using GA.Application.Features.Partners;
using GA.Application.Features.WorkOrders;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GA.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WorkOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPeriodicWorkOrderService _periodicWorkOrderService;
        private readonly INotificationService _notificationService;

        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        public WorkOrdersController(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IPeriodicWorkOrderService periodicWorkOrderService,
            INotificationService notificationService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _periodicWorkOrderService = periodicWorkOrderService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Süresi gelen periyodik şablonlardan iş emri üretmeyi hemen çalıştırır (manuel tetik).
        /// POST /api/workorders/periodic/run
        /// </summary>
        [HttpPost("periodic/run")]
        public async Task<IActionResult> RunPeriodicNow()
        {
            var result = await _periodicWorkOrderService.ProcessDueAsync();
            return Ok(new
            {
                message = "Periyodik otomasyon çalıştırıldı.",
                templatesProcessed = result.TemplatesProcessed,
                workOrdersCreated = result.WorkOrdersCreated,
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkOrders([FromQuery] string? scope, [FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var query = _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted &&
                            (isSuperAdmin ||
                             w.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && w.TenantId == _yesilPanoTenantId) ||
                             (tenantId == _yesilPanoTenantId && w.TenantId == _trugoTenantId)));

            // Mobil saha ekranı: yalnızca oturum açan kullanıcıya atanmış iş emirleri
            if (scope == "mine" && userId != Guid.Empty && !isSuperAdmin)
            {
                query = query.Where(w => w.AssignedToUserId == userId);
            }

            if (isSuperAdmin)
            {
                var partner = PartnerCatalog.Find(partnerKey) ?? PartnerCatalog.Trugo;
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

                // Bellekte filtrele: EF HashSet+ToLower çevirisi güvenilmez
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

            var workOrders = await query
                .Select(w => new {
                    id = w.Id,
                    title = w.Title,
                    description = w.Description,
                    customerName = w.CustomerName,
                    priority = w.Priority,
                    status = w.Status,
                    type = w.WorkType,
                    category = w.WorkCategory,
                    mobileDescription = w.MobileDescription,
                    address = w.Address,

                    // Zaman biçimlendirmeleri orijinal hale getirildi
                    plannedDate = w.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    startDate = w.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    endDate = w.EndDate.ToString("yyyy-MM-dd HH:mm"),

                    isPeriodic = w.IsPeriodic,
                    recurrenceInterval = w.RecurrenceInterval,
                    nextExecutionDate = w.NextExecutionDate.HasValue
                        ? w.NextExecutionDate.Value.ToString("yyyy-MM-dd HH:mm")
                        : null,

                    assignedToUserId = w.AssignedToUserId,
                    assignedToUserName = w.AssignedToUserId == null
                        ? "Atanmamış"
                        : (_context.Users.Where(u => u.Id == w.AssignedToUserId).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış"),

                    operationUserId = w.OperationUserId,
                    operationUserName = _context.Users.Where(u => u.Id == w.OperationUserId).Select(u => u.FullName).FirstOrDefault() ?? "-",

                    openedByUserId = w.OpenedByUserId,
                    openedByUserName = _context.Users.Where(u => u.Id == w.OpenedByUserId).Select(u => u.FullName).FirstOrDefault() ?? "-",

                    // 🚀 DÜZELTME: Mobil uygulamanın çökmemesi için beklediği eksik zaman damgaları eklendi
                    startedAt = w.StartedAt,
                    completedAt = w.CompletedAt,
                    cancelledAt = w.CancelledAt,

                    fieldNote = w.FieldNote,
                    fieldNoteAddedAt = w.FieldNoteAddedAt.HasValue
                        ? w.FieldNoteAddedAt.Value.ToString("yyyy-MM-dd HH:mm")
                        : null,

                    cityId = w.CityId,
                    districtId = w.DistrictId,
                    cityName = w.CityRef != null ? w.CityRef.Name : null,
                    districtName = w.DistrictRef != null ? w.DistrictRef.Name : null,

                    position = new[] { w.Location.Y, w.Location.X }
                }).ToListAsync();

            return Ok(workOrders);
        }

        [HttpGet("lookups")]
        public async Task<IActionResult> GetWorkOrderLookups([FromQuery] string? partnerKey)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;
            var partner = isSuperAdmin ? (PartnerCatalog.Find(partnerKey) ?? PartnerCatalog.Trugo) : null;

            var teamRows = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.FieldWorkerProfile)
                    .ThenInclude(f => f!.Projects)
                .Where(u => !u.IsDeleted && u.FieldWorkerProfile != null &&
                            (isSuperAdmin ||
                             u.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && u.TenantId == _yesilPanoTenantId)))
                .Select(u => new {
                    id = u.Id,
                    name = u.FullName,
                    tenantId = u.TenantId,
                    projectNames = u.FieldWorkerProfile!.Projects.Any()
                        ? u.FieldWorkerProfile.Projects.Select(p => p.Name).ToList()
                        : (string.IsNullOrWhiteSpace(u.FieldWorkerProfile.ProjectName)
                            ? new List<string>()
                            : new List<string> { u.FieldWorkerProfile.ProjectName! }),
                })
                .ToListAsync();

            if (partner != null)
            {
                teamRows = teamRows
                    .Where(t => PartnerCatalog.MatchesTeam(partner, t.tenantId, t.projectNames))
                    .ToList();
            }

            var teams = teamRows.Select(t => new { t.id, t.name }).OrderBy(t => t.name).ToList();

            var stations = await _context.Stations
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted &&
                            (isSuperAdmin ||
                             s.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && s.TenantId == _yesilPanoTenantId)))
                .Select(s => new {
                    id = s.Id,
                    name = s.Name,
                    address = s.Address,
                    city = s.City,
                    district = s.District,
                    cityId = s.CityId,
                    districtId = s.DistrictId,
                    ownerCompany = s.OwnerCompany,
                    tenantId = s.TenantId,
                    latitude = s.Location.Y,
                    longitude = s.Location.X,
                })
                .OrderBy(s => s.name)
                .ToListAsync();

            if (partner != null)
            {
                stations = stations
                    .Where(s => PartnerCatalog.Matches(partner, s.tenantId, s.ownerCompany, s.name))
                    .ToList();
            }

            var projects = await _context.Projects
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted &&
                            (isSuperAdmin ||
                             p.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && p.TenantId == _yesilPanoTenantId)))
                .Select(p => new { id = p.Id, name = p.Name, tenantId = p.TenantId })
                .OrderBy(p => p.name)
                .ToListAsync();

            if (partner != null)
            {
                projects = projects
                    .Where(p => PartnerCatalog.Matches(partner, p.tenantId, null, p.name))
                    .ToList();
            }

            return Ok(new { teams, stations, projects });
        }

        [HttpPost]
        public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;

            Guid targetTenantId = tenantId;

            // Super Admin: dto/personel tenant; istasyon bulunursa istasyon tenant'ı öncelikli
            if (isSuperAdmin)
            {
                if (dto.TenantId.HasValue && dto.TenantId.Value != Guid.Empty)
                {
                    targetTenantId = dto.TenantId.Value;
                }
                else if (dto.AssignedToUserId.HasValue && dto.AssignedToUserId.Value != Guid.Empty)
                {
                    var assignedUser = await _context.Users
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(u => u.Id == dto.AssignedToUserId.Value);

                    if (assignedUser != null)
                        targetTenantId = assignedUser.TenantId;
                }
            }

            Guid? cityId = dto.CityId;
            Guid? districtId = dto.DistrictId;
            Core.Domain.Entities.Station? resolvedStation = null;

            if (dto.StationId.HasValue && dto.StationId.Value != Guid.Empty)
            {
                resolvedStation = await _context.Stations
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == dto.StationId.Value && !s.IsDeleted);

                if (resolvedStation != null)
                {
                    var resolved = await GeoResolver.ResolveFromStationAsync(_context, resolvedStation);
                    cityId ??= resolved.CityId;
                    districtId ??= resolved.DistrictId;

                    if (string.IsNullOrWhiteSpace(dto.CustomerName))
                        dto.CustomerName = resolvedStation.Name;
                    if (string.IsNullOrWhiteSpace(dto.Address))
                        dto.Address = resolvedStation.Address;
                }
            }
            else if ((!cityId.HasValue || !districtId.HasValue) && !string.IsNullOrWhiteSpace(dto.CustomerName))
            {
                resolvedStation = await _context.Stations
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => !s.IsDeleted && s.Name.ToLower() == dto.CustomerName.Trim().ToLower());

                if (resolvedStation != null)
                {
                    var resolved = await GeoResolver.ResolveFromStationAsync(_context, resolvedStation);
                    cityId ??= resolved.CityId;
                    districtId ??= resolved.DistrictId;
                }
            }
            else
            {
                var resolved = await GeoResolver.ResolveAsync(_context, cityId, districtId, null, null);
                cityId = resolved.CityId;
                districtId = resolved.DistrictId;
            }

            // İstasyon firması (TRUGO vb.) iş emri tenant'ını belirler
            if (resolvedStation != null)
                targetTenantId = resolvedStation.TenantId;

            var workOrder = new WorkOrder
            {
                Title = dto.Title,
                Description = dto.Description,
                CustomerName = dto.CustomerName,
                Priority = dto.Priority,
                WorkType = dto.Type,
                WorkCategory = dto.Category,
                MobileDescription = dto.MobileDescription,
                Address = dto.Address,
                StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc),
                OperationUserId = dto.OperationUserId,
                OpenedByUserId = dto.OpenedByUserId ?? userId,
                AssignedToUserId = dto.AssignedToUserId,
                IsPeriodic = dto.IsPeriodic,
                RecurrenceInterval = dto.RecurrenceInterval ?? "None",
                NextExecutionDate = dto.IsPeriodic
                    ? WorkOrderRecurrence.ComputeNextExecution(
                        DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc), dto.RecurrenceInterval)
                    : null,
                Location = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 },
                CityId = cityId,
                DistrictId = districtId,

                TenantId = targetTenantId,

                Status = "Bekliyor"
            };

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyAsync(
                "WorkOrderCreated",
                "Yeni iş emri",
                $"{workOrder.CustomerName}: {workOrder.Title}",
                workOrder.TenantId,
                workOrder.Id,
                userId == Guid.Empty ? null : userId);

            return Ok(new
            {
                message = "İş emri başarıyla oluşturuldu ve saha ekiplerinin ekranına düştü!",
                id = workOrder.Id,
            });
        }

        /// <summary>
        /// Seçili noktalara aynı form alanlarıyla toplu iş emri açar. Sahacı zorunlu.
        /// POST /api/workorders/bulk
        /// </summary>
        [HttpPost("bulk")]
        public async Task<IActionResult> CreateBulkWorkOrders([FromBody] BulkCreateWorkOrderDto dto)
        {
            if (dto.StationIds == null || dto.StationIds.Count == 0)
                return BadRequest(new { message = "En az bir nokta seçilmelidir." });

            if (!dto.AssignedToUserId.HasValue || dto.AssignedToUserId == Guid.Empty)
                return BadRequest(new { message = "Sahacı ataması zorunludur." });

            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var assignee = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == dto.AssignedToUserId.Value
                                          && !u.IsDeleted && u.IsActive
                                          && u.FieldWorkerProfile != null);
            if (assignee == null)
                return BadRequest(new { message = "Seçilen saha personeli bulunamadı." });

            var stations = await _context.Stations
                .IgnoreQueryFilters()
                .Where(s => dto.StationIds.Contains(s.Id) && !s.IsDeleted)
                .ToListAsync();

            if (stations.Count == 0)
                return BadRequest(new { message = "Seçilen noktalar bulunamadı." });

            if (!isSuperAdmin)
                stations = stations.Where(s => s.TenantId == tenantId).ToList();

            var createdIds = new List<Guid>();
            foreach (var station in stations)
            {
                var resolved = await GeoResolver.ResolveFromStationAsync(_context, station);
                var start = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
                var end = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
                var title = string.IsNullOrWhiteSpace(dto.Title)
                    ? $"{station.Name} iş emri"
                    : dto.Title.Replace("{nokta}", station.Name, StringComparison.OrdinalIgnoreCase);

                var workOrder = new WorkOrder
                {
                    Title = title,
                    Description = dto.Description ?? string.Empty,
                    MobileDescription = dto.MobileDescription ?? string.Empty,
                    Address = string.IsNullOrWhiteSpace(dto.Address) ? (station.Address ?? "") : dto.Address,
                    CustomerName = station.Name,
                    Priority = string.IsNullOrWhiteSpace(dto.Priority) ? "Orta" : dto.Priority,
                    WorkType = string.IsNullOrWhiteSpace(dto.Type) ? "Arıza" : dto.Type,
                    WorkCategory = string.IsNullOrWhiteSpace(dto.Category) ? "Arıza Bildirimi" : dto.Category,
                    StartDate = start,
                    EndDate = end,
                    Location = station.Location != null
                        ? new NetTopologySuite.Geometries.Point(station.Location.X, station.Location.Y) { SRID = 4326 }
                        : new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
                    OperationUserId = dto.OperationUserId,
                    OpenedByUserId = dto.OpenedByUserId ?? userId,
                    AssignedToUserId = assignee.Id,
                    IsPeriodic = dto.IsPeriodic,
                    RecurrenceInterval = dto.IsPeriodic
                        ? (string.IsNullOrWhiteSpace(dto.RecurrenceInterval) ? "Aylik" : dto.RecurrenceInterval)
                        : "None",
                    NextExecutionDate = dto.IsPeriodic
                        ? WorkOrderRecurrence.ComputeNextExecution(start, dto.RecurrenceInterval)
                        : null,
                    CityId = resolved.CityId,
                    DistrictId = resolved.DistrictId,
                    TenantId = isSuperAdmin ? station.TenantId : (tenantId == Guid.Empty ? station.TenantId : tenantId),
                    Status = "Bekliyor",
                };

                _context.WorkOrders.Add(workOrder);
                createdIds.Add(workOrder.Id);
            }

            await _context.SaveChangesAsync();

            foreach (var id in createdIds)
            {
                var wo = await _context.WorkOrders.IgnoreQueryFilters().FirstAsync(w => w.Id == id);
                await _notificationService.NotifyAsync(
                    "WorkOrderCreated",
                    "Toplu iş emri",
                    $"{wo.CustomerName}: {wo.Title}",
                    wo.TenantId,
                    wo.Id,
                    userId == Guid.Empty ? null : userId);
            }

            return Ok(new
            {
                message = $"{createdIds.Count} iş emri oluşturuldu.",
                count = createdIds.Count,
                ids = createdIds,
            });
        }

        /// <summary>
        /// Atanmamış / yanlış atanmış iş emrine saha personeli atar veya atamayı kaldırır.
        /// PUT /api/workorders/{id}/assign
        /// </summary>
        [HttpPut("{id}/assign")]
        public async Task<IActionResult> AssignWorkOrder(Guid id, [FromBody] AssignWorkOrderDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var workOrder = await _context.WorkOrders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted &&
                                          (isSuperAdmin ||
                                           w.TenantId == tenantId ||
                                           (tenantId == _trugoTenantId && w.TenantId == _yesilPanoTenantId) ||
                                           (tenantId == _yesilPanoTenantId && w.TenantId == _trugoTenantId)));

            if (workOrder == null) return NotFound(new { message = "İş emri bulunamadı." });

            if (dto.AssignedToUserId.HasValue && dto.AssignedToUserId.Value != Guid.Empty)
            {
                var assignee = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == dto.AssignedToUserId.Value
                                              && !u.IsDeleted
                                              && u.IsActive
                                              && u.FieldWorkerProfile != null);

                if (assignee == null)
                    return BadRequest(new { message = "Seçilen saha personeli bulunamadı veya aktif değil." });

                workOrder.AssignedToUserId = assignee.Id;
                await _context.SaveChangesAsync();

                await _notificationService.NotifyAsync(
                    "WorkOrderAssigned",
                    "İş emri atandı",
                    $"{workOrder.CustomerName} → {assignee.FullName}",
                    workOrder.TenantId,
                    workOrder.Id,
                    _currentUserService.UserId == Guid.Empty ? null : _currentUserService.UserId);

                return Ok(new
                {
                    message = "İş emri personele atandı.",
                    assignedToUserId = assignee.Id,
                    assignedToUserName = assignee.FullName,
                });
            }

            workOrder.AssignedToUserId = null;
            await _context.SaveChangesAsync();

            await _notificationService.NotifyAsync(
                "WorkOrderAssigned",
                "Atama kaldırıldı",
                $"{workOrder.CustomerName}: Atanmamış",
                workOrder.TenantId,
                workOrder.Id,
                _currentUserService.UserId == Guid.Empty ? null : _currentUserService.UserId);

            return Ok(new
            {
                message = "İş emri ataması kaldırıldı.",
                assignedToUserId = (Guid?)null,
                assignedToUserName = "Atanmamış",
            });
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateWorkOrderStatusDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var workOrder = await _context.WorkOrders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted &&
                                          (isSuperAdmin ||
                                           w.TenantId == tenantId ||
                                           (tenantId == _trugoTenantId && w.TenantId == _yesilPanoTenantId)));

            if (workOrder == null) return NotFound(new { message = "İş emri bulunamadı." });

            workOrder.Status = dto.Status;

            if (!string.IsNullOrWhiteSpace(dto.FieldNote))
            {
                workOrder.FieldNote = dto.FieldNote.Trim();
                workOrder.FieldNoteAddedAt = DateTime.UtcNow;
            }

            if (dto.Status == "Tamamlandı" || dto.Status == "İptal" || dto.Status == "İptal Edildi")
            {
                workOrder.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            await _notificationService.NotifyAsync(
                "WorkOrderStatusChanged",
                "İş emri durumu güncellendi",
                $"{workOrder.CustomerName}: {workOrder.Status}",
                workOrder.TenantId,
                workOrder.Id,
                _currentUserService.UserId == Guid.Empty ? null : _currentUserService.UserId);

            return Ok(new { message = "İş emri statüsü güncellendi.", status = workOrder.Status });
        }

        [HttpPut("{id}/schedule")]
        public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] UpdateWorkOrderScheduleDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var workOrder = await _context.WorkOrders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted &&
                                          (isSuperAdmin ||
                                           w.TenantId == tenantId ||
                                           (tenantId == _trugoTenantId && w.TenantId == _yesilPanoTenantId) ||
                                           (tenantId == _yesilPanoTenantId && w.TenantId == _trugoTenantId)));

            if (workOrder == null) return NotFound(new { message = "İş emri bulunamadı." });

            var isArıza = workOrder.WorkType.Contains("Arıza", StringComparison.OrdinalIgnoreCase) ||
                          workOrder.WorkCategory.Contains("Arıza", StringComparison.OrdinalIgnoreCase);

            if (!isArıza)
            {
                workOrder.StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            }

            workOrder.EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);

            if (workOrder.EndDate < workOrder.StartDate)
            {
                return BadRequest(new { message = "Bitiş tarihi başlangıç tarihinden önce olamaz." });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "İş emri tarihleri güncellendi.",
                startDate = workOrder.StartDate.ToString("yyyy-MM-dd HH:mm"),
                endDate = workOrder.EndDate.ToString("yyyy-MM-dd HH:mm"),
            });
        }

        /// <summary>
        /// İş emri detay alanlarını günceller.
        /// PUT /api/workorders/{id}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkOrder(Guid id, [FromBody] UpdateWorkOrderDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var workOrder = await _context.WorkOrders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted &&
                                          (isSuperAdmin ||
                                           w.TenantId == tenantId ||
                                           (tenantId == _trugoTenantId && w.TenantId == _yesilPanoTenantId) ||
                                           (tenantId == _yesilPanoTenantId && w.TenantId == _trugoTenantId)));

            if (workOrder == null) return NotFound(new { message = "İş emri bulunamadı." });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Başlık zorunludur." });

            var start = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc);
            var end = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc);
            if (end < start)
                return BadRequest(new { message = "Bitiş tarihi başlangıç tarihinden önce olamaz." });

            workOrder.Title = dto.Title.Trim();
            workOrder.CustomerName = (dto.CustomerName ?? workOrder.CustomerName).Trim();
            workOrder.Description = dto.Description ?? string.Empty;
            workOrder.MobileDescription = dto.MobileDescription ?? string.Empty;
            workOrder.Address = dto.Address ?? string.Empty;
            workOrder.Priority = string.IsNullOrWhiteSpace(dto.Priority) ? workOrder.Priority : dto.Priority;
            workOrder.WorkType = string.IsNullOrWhiteSpace(dto.Type) ? workOrder.WorkType : dto.Type;
            workOrder.WorkCategory = string.IsNullOrWhiteSpace(dto.Category) ? workOrder.WorkCategory : dto.Category;
            workOrder.StartDate = start;
            workOrder.EndDate = end;
            workOrder.Location = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 };
            workOrder.OperationUserId = dto.OperationUserId;
            workOrder.OpenedByUserId = dto.OpenedByUserId;
            workOrder.AssignedToUserId = dto.AssignedToUserId;
            workOrder.IsPeriodic = dto.IsPeriodic;
            workOrder.RecurrenceInterval = dto.IsPeriodic
                ? (string.IsNullOrWhiteSpace(dto.RecurrenceInterval) ? "Aylik" : dto.RecurrenceInterval)
                : "None";
            workOrder.NextExecutionDate = dto.IsPeriodic
                ? WorkOrderRecurrence.ComputeNextExecution(start, workOrder.RecurrenceInterval)
                : null;

            if (dto.CityId.HasValue && dto.CityId != Guid.Empty)
                workOrder.CityId = dto.CityId;
            if (dto.DistrictId.HasValue && dto.DistrictId != Guid.Empty)
                workOrder.DistrictId = dto.DistrictId;

            workOrder.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var operationName = workOrder.OperationUserId == null
                ? "-"
                : await _context.Users.Where(u => u.Id == workOrder.OperationUserId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "-";
            var openedByName = workOrder.OpenedByUserId == null
                ? "-"
                : await _context.Users.Where(u => u.Id == workOrder.OpenedByUserId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "-";
            var assignedName = workOrder.AssignedToUserId == null
                ? "Atanmamış"
                : await _context.Users.Where(u => u.Id == workOrder.AssignedToUserId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "Atanmamış";

            return Ok(new
            {
                message = "İş emri güncellendi.",
                id = workOrder.Id,
                title = workOrder.Title,
                customerName = workOrder.CustomerName,
                description = workOrder.Description,
                mobileDescription = workOrder.MobileDescription,
                address = workOrder.Address,
                priority = workOrder.Priority,
                type = workOrder.WorkType,
                category = workOrder.WorkCategory,
                startDate = workOrder.StartDate.ToString("yyyy-MM-dd HH:mm"),
                endDate = workOrder.EndDate.ToString("yyyy-MM-dd HH:mm"),
                position = new[] { workOrder.Location.Y, workOrder.Location.X },
                operationUserId = workOrder.OperationUserId,
                operationUserName = operationName,
                openedByUserId = workOrder.OpenedByUserId,
                openedByUserName = openedByName,
                assignedToUserId = workOrder.AssignedToUserId,
                assignedToUserName = assignedName,
                isPeriodic = workOrder.IsPeriodic,
                recurrenceInterval = workOrder.RecurrenceInterval,
                nextExecutionDate = workOrder.NextExecutionDate.HasValue
                    ? workOrder.NextExecutionDate.Value.ToString("yyyy-MM-dd HH:mm")
                    : null,
            });
        }
    }

    public class CreateWorkOrderDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MobileDescription { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Priority { get; set; } = "Orta";
        public string Type { get; set; } = "Saha";
        public string Category { get; set; } = "Arıza Bildirimi";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Guid? OperationUserId { get; set; }
        public Guid? OpenedByUserId { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public bool IsPeriodic { get; set; }
        public string RecurrenceInterval { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? StationId { get; set; }
        public Guid? CityId { get; set; }
        public Guid? DistrictId { get; set; }
    }

    public class BulkCreateWorkOrderDto
    {
        public List<Guid> StationIds { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MobileDescription { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Priority { get; set; } = "Orta";
        public string Type { get; set; } = "Arıza";
        public string Category { get; set; } = "Arıza Bildirimi";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Guid? OperationUserId { get; set; }
        public Guid? OpenedByUserId { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public bool IsPeriodic { get; set; }
        public string RecurrenceInterval { get; set; } = "None";
    }

    public class AssignWorkOrderDto
    {
        public Guid? AssignedToUserId { get; set; }
    }

    public class UpdateWorkOrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public string? FieldNote { get; set; }
    }

    public class UpdateWorkOrderScheduleDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class UpdateWorkOrderDto
    {
        public string Title { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MobileDescription { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Priority { get; set; } = "Orta";
        public string Type { get; set; } = "Arıza";
        public string Category { get; set; } = "Arıza Bildirimi";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public Guid? OperationUserId { get; set; }
        public Guid? OpenedByUserId { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public bool IsPeriodic { get; set; }
        public string RecurrenceInterval { get; set; } = "None";
        public Guid? CityId { get; set; }
        public Guid? DistrictId { get; set; }
    }
}