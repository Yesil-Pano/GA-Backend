using GA.Application.Features.Common;
using GA.Core.Domain.Entities;

namespace GA.Application.Features.Notifications
{
    public static class WorkOrderNotificationMessages
    {
        public static string Body(WorkOrder workOrder)
        {
            var start = TurkeyTime.Format(workOrder.StartDate);
            return $"{workOrder.CustomerName}: {workOrder.Title} · Başlangıç: {start}";
        }

        public static string CreatedTitle(string tenantName, bool actorIsTenantWebUser)
        {
            return actorIsTenantWebUser
                ? $"{tenantName} — Web kullanıcısı iş emri açtı"
                : $"{tenantName} — Yeni iş emri";
        }

        public static string CreatedBody(WorkOrder workOrder, string? actorFullName)
        {
            var detail = Body(workOrder);
            return string.IsNullOrWhiteSpace(actorFullName) ? detail : $"{actorFullName}: {detail}";
        }
    }
}
