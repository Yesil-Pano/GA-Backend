using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GA.Application.Features.WorkOrders
{
    public class PeriodicRepairResult
    {
        public int PeriodsCreated { get; set; }
        public int TemplatesProcessed { get; set; }
        public int FutureCompletionsReset { get; set; }
    }

    public class AssignmentRepairResult
    {
        public int AffectedCount { get; set; }
        public int RepairedCount { get; set; }
        public bool DryRun { get; set; }
    }

    public interface IWorkOrderRepairService
    {
        Task<PeriodicRepairResult> RepairPeriodicAsync(CancellationToken cancellationToken = default);
        Task<AssignmentRepairResult> RepairAssignmentFieldsAsync(
            Guid? defaultOperationUserId,
            bool dryRun,
            CancellationToken cancellationToken = default);
    }

    public class WorkOrderRepairService : IWorkOrderRepairService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPeriodicScheduleService _periodicScheduleService;
        private readonly ILogger<WorkOrderRepairService> _logger;

        public WorkOrderRepairService(
            ApplicationDbContext context,
            IPeriodicScheduleService periodicScheduleService,
            ILogger<WorkOrderRepairService> logger)
        {
            _context = context;
            _periodicScheduleService = periodicScheduleService;
            _logger = logger;
        }

        public async Task<PeriodicRepairResult> RepairPeriodicAsync(CancellationToken cancellationToken = default)
        {
            var result = new PeriodicRepairResult();
            var backfill = await _periodicScheduleService.BackfillAllTemplatesAsync(cancellationToken);
            result.PeriodsCreated = backfill.PeriodsCreated;
            result.TemplatesProcessed = backfill.TemplatesProcessed;

            var nowUtc = DateTime.UtcNow;
            var (_, monthEndUtc) = WorkOrderMobileVisibility.GetTurkeyCurrentMonthUtcBounds(nowUtc);

            var futureCompleted = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted
                            && w.ParentWorkOrderId != null
                            && w.Status == WorkOrderStatus.Completed
                            && w.StartDate >= monthEndUtc
                            && !w.WorkType.Contains("Arıza")
                            && !w.WorkType.Contains("Ariza"))
                .ToListAsync(cancellationToken);

            foreach (var period in futureCompleted)
            {
                period.Status = WorkOrderStatus.Waiting;
                period.CompletedAt = null;
                period.StartedAt = null;
                period.UpdatedAt = nowUtc;
                result.FutureCompletionsReset++;
            }

            if (result.FutureCompletionsReset > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Periyodik onarım: {Count} erken kapatılmış gelecek dönem Bekliyor'a alındı.",
                    result.FutureCompletionsReset);
            }

            return result;
        }

        public async Task<AssignmentRepairResult> RepairAssignmentFieldsAsync(
            Guid? defaultOperationUserId,
            bool dryRun,
            CancellationToken cancellationToken = default)
        {
            var result = new AssignmentRepairResult { DryRun = dryRun };

            if (defaultOperationUserId.HasValue && defaultOperationUserId.Value != Guid.Empty)
            {
                var validOps = await WorkOrderOperationSupervisors.IsValidYesilPanoSupervisorAsync(
                    _context, defaultOperationUserId.Value, cancellationToken);

                if (!validOps)
                    throw new InvalidOperationException("Varsayılan operasyon sorumlusu geçerli bir Yeşil Pano operasyoncu değil.");
            }

            var fieldWorkerIds = await _context.FieldWorkerProfiles
                .IgnoreQueryFilters()
                .Where(f => !f.IsDeleted)
                .Select(f => f.UserId)
                .ToListAsync(cancellationToken);

            var fieldWorkerSet = fieldWorkerIds.ToHashSet();

            var affected = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted
                            && w.AssignedToUserId != null
                            && w.OperationUserId == w.AssignedToUserId
                            && fieldWorkerSet.Contains(w.AssignedToUserId.Value))
                .ToListAsync(cancellationToken);

            result.AffectedCount = affected.Count;

            if (dryRun || affected.Count == 0)
                return result;

            var nowUtc = DateTime.UtcNow;
            foreach (var workOrder in affected)
            {
                workOrder.OperationUserId = defaultOperationUserId.HasValue && defaultOperationUserId.Value != Guid.Empty
                    ? defaultOperationUserId
                    : null;
                workOrder.UpdatedAt = nowUtc;
                result.RepairedCount++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Atama alanı onarımı: {Repaired}/{Affected} kayıt güncellendi.",
                result.RepairedCount,
                result.AffectedCount);

            return result;
        }
    }
}
