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
        public async Task<IActionResult> GetWorkOrders()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var workOrders = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted &&
                            (isSuperAdmin ||
                             w.TenantId == tenantId ||
                             (tenantId == _trugoTenantId && w.TenantId == _yesilPanoTenantId) ||
                             (tenantId == _yesilPanoTenantId && w.TenantId == _trugoTenantId)))
                .Select(w => new {
                    id = w.Id,
                    title = w.Title,
                    description = w.Description,
                    customerName = w.CustomerName,
                    priority = w.Priority,
                    status = w.Status,
                    type = w.WorkType,
                    // 🚀 DETAY EKRANI İÇİN EKSİK ALANLAR SEÇİLDİ
                    category = w.WorkCategory,
                    mobileDescription = w.MobileDescription,
                    address = w.Address,
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

            if (tenantId == _trugoTenantId) targetTenantId = _yesilPanoTenantId;
            else if (isSuperAdmin && dto.TenantId.HasValue && dto.TenantId.Value != Guid.Empty) targetTenantId = dto.TenantId.Value;

            var workOrder = new WorkOrder
            {
                Title = dto.Title,
                Description = dto.Description,
                CustomerName = dto.CustomerName,
                Priority = dto.Priority,
                WorkType = dto.Type,
                // 🚀 EKSİK OLAN TÜM EŞLEŞTİRMELER (MAPPINGS) EKLENDİ
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
                TenantId = targetTenantId,
                Status = "Bekliyor"
            };

            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();

            return Ok(new { message = "İş emri başarıyla oluşturuldu ve doğrudan saha ekiplerinin ekranına düşürüldü!" });
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

            if (dto.Status == "Tamamlandı" || dto.Status == "İptal Edildi")
                workOrder.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "İş emri statüsü güncellendi." });
        }
    }

    // 🚀 DTO EKSİKLERİ GİDERİLDİ
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
    }
}