using GA.Application.Features.Common;
using GA.Core.Domain.Entities;

namespace GA.Application.Features.WorkOrders
{
    /// <summary>Periyodik dönem tarih kuralları (Türkiye takvim ayı).</summary>
    public static class WorkOrderPeriodRules
    {
        public static bool IsPeriodicContext(WorkOrder workOrder) =>
            workOrder.IsPeriodic || workOrder.ParentWorkOrderId.HasValue;

        public static bool IsTrueArızaWorkOrder(WorkOrder workOrder) =>
            WorkOrderMobileVisibility.IsArızaWorkOrder(workOrder)
            && !workOrder.IsPeriodic
            && !workOrder.ParentWorkOrderId.HasValue;

        public static bool IsFutureTurkeyPeriod(DateTime startDateUtc, DateTime nowUtc)
        {
            var (monthStartUtc, monthEndUtc) = WorkOrderMobileVisibility.GetTurkeyCurrentMonthUtcBounds(nowUtc);
            return startDateUtc >= monthEndUtc;
        }

        /// <summary>Saha Tamamlandı/İptal: periyodik dönem henüz gelmediyse engelle.</summary>
        public static string? ValidateFieldPeriodClose(WorkOrder workOrder, string targetStatus, DateTime nowUtc)
        {
            if (!WorkOrderStatus.IsTerminal(targetStatus))
                return null;

            if (IsTrueArızaWorkOrder(workOrder))
                return null;

            if (!IsPeriodicContext(workOrder))
                return null;

            if (IsFutureTurkeyPeriod(workOrder.StartDate, nowUtc))
            {
                return "Bu dönemin vadesi henüz gelmedi. Yalnızca bulunulan veya geçmiş dönem kapatılabilir.";
            }

            return null;
        }
    }
}
