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

        private readonly Guid _yesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private readonly Guid _trugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        public WorkOrdersController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkOrders([FromQuery] string? scope)
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

            // 🚀 Mobil Uygulama Filtresi: "Sadece bana atananları getir"
            if (scope == "mine" && userId != Guid.Empty && !isSuperAdmin)
            {
                query = query.Where(w => w.AssignedToUserId == userId);
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

                    assignedToUserId = w.AssignedToUserId,
                    assignedToUserName = _context.Users.Where(u => u.Id == w.AssignedToUserId).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış",

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

                    position = new[] { w.Location.Y, w.Location.X }
                }).ToListAsync();

            return Ok(workOrders);
        }

        [HttpGet("lookups")]
        public async Task<IActionResult> GetWorkOrderLookups()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var teams = await _context.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsDeleted && u.FieldWorkerProfile != null &&
                            (isSuperAdmin ||
                             u.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && u.TenantId == _yesilPanoTenantId)))
                .Select(u => new { id = u.Id, name = u.FullName })
                .ToListAsync();

            var stations = await _context.Stations
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted &&
                            (isSuperAdmin ||
                             s.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && s.TenantId == _yesilPanoTenantId)))
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Ok(new { teams, stations });
        }

        [HttpPost]
        public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;

            Guid targetTenantId = tenantId;

            if (tenantId == _trugoTenantId)
            {
                targetTenantId = _yesilPanoTenantId;
            }
            else if (isSuperAdmin)
            {
                if (dto.TenantId.HasValue && dto.TenantId.Value != Guid.Empty)
                {
                    targetTenantId = dto.TenantId.Value;
                }
                // 🚀 BÜYÜK SİHİR: Eğer Admin firma seçmemişse ama bir personel atamışsa, 
                // o personelin hangi firmada olduğuna (TenantId) bak ve iş emrini otomatik olarak oraya zimmetle!
                else if (dto.AssignedToUserId.HasValue && dto.AssignedToUserId.Value != Guid.Empty)
                {
                    var assignedUser = await _context.Users
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(u => u.Id == dto.AssignedToUserId.Value);

                    if (assignedUser != null)
                    {
                        targetTenantId = assignedUser.TenantId;
                    }
                }
            }

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
                Location = new NetTopologySuite.Geometries.Point(dto.Longitude, dto.Latitude) { SRID = 4326 },

                TenantId = targetTenantId, // 🔒 Artık "Guid.Empty" değil, Yasin'in firmasına (Yeşil Pano'ya) başarıyla zimmetlendi!

                Status = "Bekliyor"
            };

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            return Ok(new { message = "İş emri başarıyla oluşturuldu ve saha ekiplerinin ekranına düştü!" });
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
}