using GA.Core.Domain.Entities;

namespace GA.Application.Features.WorkOrders
{
    public static class WorkOrderStatus
    {
        public const string Unassigned = "Atanmamış";
        public const string Waiting = "Bekliyor";
        public const string InProgress = "Devam Ediyor";
        public const string Completed = "Tamamlandı";
        public const string Cancelled = "İptal";
        public const string CancelledAlt = "İptal Edildi";

        public static string ResolveForCreate(bool hasAssignee) =>
            hasAssignee ? Waiting : Unassigned;

        /// <summary>Atama / yeniden atama: yalnızca saha henüz başlamadıysa Bekliyor.</summary>
        public static void ApplyOnAssign(WorkOrder workOrder)
        {
            if (!workOrder.StartedAt.HasValue)
                workOrder.Status = Waiting;
        }

        /// <summary>Atama kaldırma: saha başlamadıysa Atanmamış.</summary>
        public static void ApplyOnUnassign(WorkOrder workOrder)
        {
            if (!workOrder.StartedAt.HasValue)
                workOrder.Status = Unassigned;
        }

        public static bool IsTerminal(string status) =>
            string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CancelledAlt, StringComparison.OrdinalIgnoreCase);

        /// <summary>Geçersiz geçişte hata mesajı; geçerliyse null.</summary>
        public static string? ValidateFieldTransition(string currentStatus, string nextStatus)
        {
            var current = (currentStatus ?? string.Empty).Trim();
            var next = (nextStatus ?? string.Empty).Trim();

            if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
                return null;

            if (IsTerminal(current))
                return "Tamamlanmış veya iptal edilmiş iş emrinin durumu değiştirilemez.";

            if (string.Equals(current, Waiting, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(next, InProgress, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(next, Cancelled, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(next, CancelledAlt, StringComparison.OrdinalIgnoreCase))
                    return null;

                return "Bekleyen iş emri yalnızca 'Devam Ediyor' veya 'İptal' durumuna geçirilebilir.";
            }

            if (string.Equals(current, InProgress, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(next, Completed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(next, Cancelled, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(next, CancelledAlt, StringComparison.OrdinalIgnoreCase))
                    return null;

                return "Devam eden iş emri yalnızca 'Tamamlandı' veya 'İptal' durumuna geçirilebilir.";
            }

            if (string.Equals(current, Unassigned, StringComparison.OrdinalIgnoreCase))
                return "Atanmamış iş emri üzerinde saha durum güncellemesi yapılamaz.";

            return $"Geçersiz durum geçişi: {current} → {next}";
        }
    }
}
