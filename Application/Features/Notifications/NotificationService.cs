using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.Notifications
{
    public interface INotificationService
    {
        Task NotifyAsync(
            string type,
            string title,
            string message,
            Guid tenantId,
            Guid? workOrderId = null,
            Guid? actorUserId = null,
            CancellationToken cancellationToken = default);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task NotifyAsync(
            string type,
            string title,
            string message,
            Guid tenantId,
            Guid? workOrderId = null,
            Guid? actorUserId = null,
            CancellationToken cancellationToken = default)
        {
            _context.AppNotifications.Add(new AppNotification
            {
                Type = type,
                Title = title,
                Message = message,
                TenantId = tenantId,
                WorkOrderId = workOrderId,
                ActorUserId = actorUserId,
                IsRead = false,
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
