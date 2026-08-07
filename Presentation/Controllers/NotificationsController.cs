using GA.Core.Domain.Entities;
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

        private static readonly string[] OfficeNotificationTypes =
        [
            "WorkOrderAssigned",
            "WorkOrderCreated",
            "WorkOrderPeriodic",
        ];

        public NotificationsController(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// GET /api/notifications
        /// scope=office → web paneli (atama akışı, tenant geneli).
        /// scope=personal veya boş → yalnızca TargetUserId == oturum açan kullanıcı (mobil zil).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int take = 10,
            [FromQuery] string? type = null,
            [FromQuery] string? scope = null)
        {
            take = Math.Clamp(take, 1, 100);
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;
            var isOfficeScope = string.Equals(scope, "office", StringComparison.OrdinalIgnoreCase);

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

            if (isOfficeScope && !isFieldWorker)
            {
                if (!isSuperAdmin)
                    query = query.Where(n => n.TenantId == tenantId);

                query = ApplyOfficeRecipientFilter(query, userId);

                if (string.IsNullOrWhiteSpace(type))
                    query = query.Where(n => OfficeNotificationTypes.Contains(n.Type));
                else
                    query = query.Where(n => n.Type == type);
            }
            else
            {
                query = query.Where(n => n.TargetUserId == userId);

                if (!string.IsNullOrWhiteSpace(type))
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

            if (isOfficeScope && !isFieldWorker)
            {
                if (!isSuperAdmin)
                    unreadQuery = unreadQuery.Where(n => n.TenantId == tenantId);

                unreadQuery = ApplyOfficeRecipientFilter(unreadQuery, userId);
                unreadQuery = unreadQuery.Where(n => OfficeNotificationTypes.Contains(n.Type));
            }
            else
            {
                unreadQuery = unreadQuery.Where(n => n.TargetUserId == userId);
            }

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

            if (!CanAccessNotification(n, userId, tenantId, isSuperAdmin))
                return NotFound();

            n.IsRead = true;
            n.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Okundu." });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead([FromQuery] string? scope = null)
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            var isSuperAdmin = tenantId == Guid.Empty;
            var isOfficeScope = string.Equals(scope, "office", StringComparison.OrdinalIgnoreCase);

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

            if (isOfficeScope && !isFieldWorker)
            {
                if (!isSuperAdmin)
                    q = q.Where(n => n.TenantId == tenantId);

                q = ApplyOfficeRecipientFilter(q, userId);
            }
            else
            {
                q = q.Where(n => n.TargetUserId == userId);
            }

            var list = await q.ToListAsync();
            foreach (var n in list)
            {
                n.IsRead = true;
                n.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Tümü okundu.", count = list.Count });
        }

        /// <summary>
        /// Web paneli: tenant geneli (TargetUserId null) veya doğrudan bu kullanıcıya hedeflenen kayıtlar.
        /// Super Admin'lerin birbirinin kişisel bildirimlerini görmemesi için gerekli.
        /// </summary>
        private static IQueryable<AppNotification> ApplyOfficeRecipientFilter(
            IQueryable<AppNotification> query,
            Guid userId)
        {
            return query.Where(n => n.TargetUserId == null || n.TargetUserId == userId);
        }

        private static bool CanAccessNotification(
            AppNotification notification,
            Guid userId,
            Guid tenantId,
            bool isSuperAdmin)
        {
            if (notification.TargetUserId == userId)
                return true;

            if (notification.TargetUserId.HasValue)
                return false;

            return isSuperAdmin || notification.TenantId == tenantId;
        }
    }
}
