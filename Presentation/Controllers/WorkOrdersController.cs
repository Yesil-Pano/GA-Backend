using GA.Application.Features.Auth.DTOs;
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
    public class WorkOrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public WorkOrdersController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkOrders()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty; // 🚀 VIP KONTROLÜ

            var orders = await _context.WorkOrders
                .Where(w => !w.IsDeleted && (isSuperAdmin || w.TenantId == tenantId))
                .Select(w => new {
                    id = w.Id,
                    title = w.Title,
                    customerName = w.CustomerName,
                    priority = w.Priority,
                    status = w.Status,
                    type = w.WorkType,
                    category = w.WorkCategory,
                    description = w.Description,
                    mobileDescription = w.MobileDescription,
                    address = w.Address,
                    startDate = w.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    endDate = w.EndDate.ToString("yyyy-MM-dd HH:mm"),
                    plannedDate = w.StartDate.ToString("yyyy-MM-dd HH:mm"),
                    position = new[] { w.Location.Y, w.Location.X },
                    assignedToUserId = w.AssignedToUserId,
                    operationUserName = _context.Users.Where(u => u.Id == w.OperationUserId && (isSuperAdmin || u.TenantId == tenantId)).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış",
                    openedByUserName = _context.Users.Where(u => u.Id == w.OpenedByUserId && (isSuperAdmin || u.TenantId == tenantId)).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış",
                    assignedToUserName = _context.Users.Where(u => u.Id == w.AssignedToUserId && (isSuperAdmin || u.TenantId == tenantId)).Select(u => u.FullName).FirstOrDefault() ?? "Atanmamış",
                    startedAt   = w.StartedAt,
                    completedAt = w.CompletedAt,
                    cancelledAt = w.CancelledAt,
                }).ToListAsync();
            return Ok(orders);
        }

        [HttpGet("lookups")]
        public async Task<IActionResult> GetFormLookups()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty; // 🚀 VIP KONTROLÜ

            var systemPersonnel = await _context.Users
                .Where(u => u.IsActive && !u.IsDeleted && (isSuperAdmin || u.TenantId == tenantId))
                .Select(u => new { id = u.Id, fullName = u.FullName })
                .ToListAsync();

            var workTypes = new[] { "Arıza", "Devreye Alma", "Kontrol" };
            var workCategories = new[] { "Arıza Bildirimi", "YG İşletme Sorumluluğu Talebi", "YG Bakım", "AG Bakım", "Kapasitif Ceza", "QR, Etiket ve Görsel Kontrol" };

            return Ok(new { personnel = systemPersonnel, types = workTypes, categories = workCategories });
        }

        [HttpPost]
        public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == Guid.Empty) return Unauthorized(new { Message = "Sistem Yöneticileri doğrudan iş emri açamaz, bir firma seçmelidir." });

            if (dto.AssignedToUserId.HasValue)
            {
                var isPersonnelValid = await _context.Users.AnyAsync(u => u.Id == dto.AssignedToUserId && u.TenantId == tenantId && !u.IsDeleted);
                if (!isPersonnelValid) return BadRequest(new { Message = "HATA: Atamaya çalıştığınız personel sizin firmanıza ait değil!" });
            }

            var workOrder = new WorkOrder
            {
                Title = dto.Title,
                CustomerName = dto.CustomerName,
                Description = dto.Description,
                MobileDescription = dto.MobileDescription,
                Address = dto.Address,
                Priority = dto.Priority,
                WorkType = dto.WorkType,
                WorkCategory = dto.WorkCategory,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 },
                OperationUserId = dto.OperationUserId,
                OpenedByUserId = dto.OpenedByUserId,
                AssignedToUserId = dto.AssignedToUserId,
                TenantId = tenantId
            };
            _context.WorkOrders.Add(workOrder);
            await _context.SaveChangesAsync();
            return Ok(new { message = "İş emri başarıyla oluşturuldu!" });
        }

        /// <summary>
        /// Mobil uygulamadan iş emri durumunu günceller.
        /// Geçerli geçişler: Bekliyor → Devam Ediyor | Bekliyor/Devam Ediyor → Tamamlandı | İptal
        /// PATCH /api/workorders/{id}/status
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateWorkOrderStatusDto dto)
        {
            var tenantId = _currentUserService.TenantId;
            var userId   = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var workOrder = await _context.WorkOrders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);

            if (workOrder == null) return NotFound(new { message = "İş emri bulunamadı." });
            if (!isSuperAdmin && workOrder.TenantId != tenantId) return Forbid();

            // Sadece atanan kişi veya admin güncelleyebilir
            if (!isSuperAdmin && workOrder.AssignedToUserId != userId)
                return StatusCode(403, new { message = "Bu iş emrini yalnızca atanan kişi güncelleyebilir." });

            // Geçerli durum geçişleri
            var validTransitions = new Dictionary<string, string[]>
            {
                { "Bekliyor",     new[] { "Devam Ediyor", "İptal" } },
                { "Devam Ediyor", new[] { "Tamamlandı",   "İptal" } },
            };

            if (!validTransitions.TryGetValue(workOrder.Status, out var allowed) || !allowed.Contains(dto.Status))
                return BadRequest(new { message = $"'{workOrder.Status}' → '{dto.Status}' geçişi geçersiz." });

            workOrder.Status    = dto.Status;
            workOrder.UpdatedAt = DateTime.UtcNow;

            switch (dto.Status)
            {
                case "Devam Ediyor": workOrder.StartedAt   = DateTime.UtcNow; break;
                case "Tamamlandı":   workOrder.CompletedAt = DateTime.UtcNow; break;
                case "İptal":        workOrder.CancelledAt = DateTime.UtcNow; break;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Durum güncellendi.", status = workOrder.Status });
        }
    }
}