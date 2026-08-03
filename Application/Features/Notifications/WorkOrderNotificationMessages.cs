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
    }
}
