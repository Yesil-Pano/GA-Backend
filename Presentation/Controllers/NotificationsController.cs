using GA.Application.Features.Notifications;
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

        /// <summary>GET /api/notifications — Super Admin tümü; tenant kendi kayıtları.</summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int take = 40)
        {
            take = Math.Clamp(take, 1, 100);
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var query = _context.AppNotifications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(n => !n.IsDeleted);

            if (!isSuperAdmin)
                query = query.Where(n => n.TenantId == tenantId);

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
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt,
                    tenantId = n.TenantId,
                })
                .ToListAsync();

            var unread = await query.CountAsync(n => !n.IsRead);

            return Ok(new { unread, items });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var n = await _context.AppNotifications
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted &&
                                          (isSuperAdmin || x.TenantId == tenantId));

            if (n == null) return NotFound();

            n.IsRead = true;
            n.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Okundu." });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var tenantId = _currentUserService.TenantId;
            var isSuperAdmin = tenantId == Guid.Empty;

            var q = _context.AppNotifications
                .IgnoreQueryFilters()
                .Where(n => !n.IsDeleted && !n.IsRead);

            if (!isSuperAdmin)
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
