using GA.Core.Domain.Entities;
using System.Linq;
using System.Runtime.InteropServices;

namespace GA.Application.Features.WorkOrders
{
    /// <summary>Mobil saha listesi görünürlük kuralları.</summary>
    public static class WorkOrderMobileVisibility
    {
        private static TimeZoneInfo TurkeyTimeZone =>
            TimeZoneInfo.FindSystemTimeZoneById(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "Turkey Standard Time"
                    : "Europe/Istanbul");

        public static bool IsArızaWorkOrder(string? workType, string? workCategory)
        {
            var combined = $"{workType} {workCategory}";
            return combined.Contains("Arıza", StringComparison.OrdinalIgnoreCase)
                   || combined.Contains("Ariza", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsArızaWorkOrder(WorkOrder workOrder) =>
            IsArızaWorkOrder(workOrder.WorkType, workOrder.WorkCategory);

        /// <summary>Türkiye takvim ayının UTC başlangıç/bitiş sınırları [start, end).</summary>
        public static (DateTime MonthStartUtc, DateTime MonthEndUtc) GetTurkeyCurrentMonthUtcBounds(DateTime nowUtc)
        {
            var turkeyNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TurkeyTimeZone);
            var monthStartLocal = new DateTime(turkeyNow.Year, turkeyNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var monthEndLocal = monthStartLocal.AddMonths(1);
            return (
                TimeZoneInfo.ConvertTimeToUtc(monthStartLocal, TurkeyTimeZone),
                TimeZoneInfo.ConvertTimeToUtc(monthEndLocal, TurkeyTimeZone));
        }

        /// <summary>
        /// Tamamlanan/iptal: süresiz görünür.
        /// Arıza: aktifken her zaman görünür.
        /// Diğerleri: yalnızca StartDate bulunulan Türkiye ayında.
        /// </summary>
        public static bool MatchesMobileListFilter(
            string status,
            string? workType,
            string? workCategory,
            DateTime startDate,
            DateTime monthStartUtc,
            DateTime monthEndUtc)
        {
            if (WorkOrderStatus.IsTerminal(status))
                return true;

            if (IsArızaWorkOrder(workType, workCategory))
                return true;

            return startDate >= monthStartUtc && startDate < monthEndUtc;
        }

        /// <summary>EF Core SQL çevirisi için MatchesMobileListFilter ile aynı mantık.</summary>
        public static IQueryable<WorkOrder> ApplyMobileListFilter(
            this IQueryable<WorkOrder> query,
            DateTime monthStartUtc,
            DateTime monthEndUtc) =>
            query.Where(w =>
                (w.Status == WorkOrderStatus.Completed
                    || w.Status == WorkOrderStatus.Cancelled
                    || w.Status == WorkOrderStatus.CancelledAlt)
                || w.WorkType.Contains("Arıza")
                || w.WorkType.Contains("Ariza")
                || w.WorkCategory.Contains("Arıza")
                || w.WorkCategory.Contains("Ariza")
                || (w.StartDate >= monthStartUtc && w.StartDate < monthEndUtc));

        /// <summary>Yeniden atamada arıza penceresini şimdi + 24 saat olarak günceller.</summary>
        public static void RefreshArızaScheduleOnAssign(WorkOrder workOrder, DateTime nowUtc)
        {
            if (!IsArızaWorkOrder(workOrder))
                return;
            if (WorkOrderStatus.IsTerminal(workOrder.Status))
                return;

            workOrder.StartDate = nowUtc;
            workOrder.EndDate = nowUtc.AddHours(24);
        }
    }
}
