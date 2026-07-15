using GA.Core.Domain.Common;
using GA.Core.Interfaces;

namespace GA.Core.Domain.Entities
{
    /// <summary>
    /// Uygulama içi bildirim (yeni iş, atama, durum, periyodik üretim).
    /// </summary>
    public class AppNotification : BaseEntity, IMultiTenant
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        /// <summary>WorkOrderCreated | WorkOrderAssigned | WorkOrderStatusChanged | WorkOrderPeriodic</summary>
        public string Type { get; set; } = string.Empty;
        public Guid? WorkOrderId { get; set; }
        public Guid? ActorUserId { get; set; }
        public Guid TenantId { get; set; }
        public Guid? CustomerId { get; set; }
        public bool IsRead { get; set; }
    }
}
