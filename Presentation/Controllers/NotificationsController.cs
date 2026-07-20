using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GA.Presentation.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public NotificationsController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// GET /api/notifications
        /// Super Admin / ofis: son atama bildirimleri (varsayılan 10).
        /// Sahacı: yalnızca kendisine (TargetUserId) gelenler.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int take = 10,
            [FromQuery] string? type = null)
        {
            take = Math.Clamp(take, 1, 100);
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var isFieldWorker = false;
            if (!isSuperAdmin && userId != Guid.Empty)
            {
                isFieldWorker = await _context.Users
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == userId && !u.IsDeleted && u.FieldWorkerProfile != null);
            }

            var query = _context.AppNotifications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(n => !n.IsDeleted);

            if (isFieldWorker)
            {
                // Sadece bu sahacıya hedeflenen bildirimler
                query = query.Where(n => n.TargetUserId == userId);
            }
            else if (!isSuperAdmin)
            {
                query = query.Where(n => n.TenantId == tenantId);
            }

            // Web paneli: son iş emri atama bildirimleri
            if (!isFieldWorker)
            {
                if (string.IsNullOrWhiteSpace(type))
                    query = query.Where(n => n.Type == "WorkOrderAssigned" || n.Type == "WorkOrderCreated");
                else
                    query = query.Where(n => n.Type == type);
            }
            else if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(n => n.Type == type);
            }

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type,
                    workOrderId = n.WorkOrderId,
                    targetUserId = n.TargetUserId,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt,
                    tenantId = n.TenantId,
                })
                .ToListAsync();

            var unreadQuery = _context.AppNotifications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(n => !n.IsDeleted && !n.IsRead);

            if (isFieldWorker)
                unreadQuery = unreadQuery.Where(n => n.TargetUserId == userId);
            else if (!isSuperAdmin)
                unreadQuery = unreadQuery.Where(n => n.TenantId == tenantId);

            if (!isFieldWorker)
                unreadQuery = unreadQuery.Where(n => n.Type == "WorkOrderAssigned" || n.Type == "WorkOrderCreated");

            var unread = await unreadQuery.CountAsync();

            return Ok(new { unread, items });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var n = await _context.AppNotifications
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (n == null) return NotFound();

            if (!isSuperAdmin)
            {
                var isTarget = n.TargetUserId == userId;
                var sameTenant = n.TenantId == tenantId;
                if (!isTarget && !sameTenant) return NotFound();
            }

            n.IsRead = true;
            n.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Okundu." });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var isFieldWorker = false;
            if (!isSuperAdmin && userId != Guid.Empty)
            {
                isFieldWorker = await _context.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Id == userId && !u.IsDeleted && u.FieldWorkerProfile != null);
            }

            var q = _context.AppNotifications
                .IgnoreQueryFilters()
                .Where(n => !n.IsDeleted && !n.IsRead);

            if (isFieldWorker)
                q = q.Where(n => n.TargetUserId == userId);
            else if (!isSuperAdmin)
                q = q.Where(n => n.TenantId == tenantId);

            var list = await q.ToListAsync();
            foreach (var n in list)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = "Tümü okundu.", count = list.Count });
        }
    }
}
