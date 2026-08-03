using GA.Application.Features.Common;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace GA.Application.Features.WorkOrders
{
    public class PeriodicBackfillResult
    {
        public int TemplatesProcessed { get; set; }
        public int PeriodsCreated { get; set; }
        public int PeriodLabelsUpdated { get; set; }
    }

    public interface IPeriodicScheduleService
    {
        /// <summary>Şablon için yıl sonuna kadar eksik dönem kayıtlarını oluşturur (1. dönem = şablon).</summary>
        Task<int> EnsureYearPeriodsAsync(WorkOrder template, CancellationToken cancellationToken = default);

        /// <summary>Tüm periyodik şablonlar için backfill.</summary>
        Task<PeriodicBackfillResult> BackfillAllTemplatesAsync(CancellationToken cancellationToken = default);

        Guid ResolveTemplateId(WorkOrder workOrder);
    }

    public class PeriodicScheduleService : IPeriodicScheduleService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PeriodicScheduleService> _logger;

        public PeriodicScheduleService(
            ApplicationDbContext context,
            ILogger<PeriodicScheduleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public Guid ResolveTemplateId(WorkOrder workOrder)
        {
            if (workOrder.ParentWorkOrderId.HasValue)
                return workOrder.ParentWorkOrderId.Value;
            return workOrder.Id;
        }

        public async Task<int> EnsureYearPeriodsAsync(WorkOrder template, CancellationToken cancellationToken = default)
        {
            if (!template.IsPeriodic || template.ParentWorkOrderId.HasValue)
                return 0;

            var calendarYear = GetLocalYear(template.StartDate);
            EnsureTemplatePeriodLabel(template, calendarYear);

            var duration = WorkOrderRecurrence.ResolveDuration(template.StartDate, template.EndDate);
            var existingStarts = await _context.WorkOrders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => !w.IsDeleted &&
                            (w.Id == template.Id || w.ParentWorkOrderId == template.Id))
                .Select(w => w.StartDate)
                .ToListAsync(cancellationToken);

            var created = 0;
            var cursorStart = DateTime.SpecifyKind(template.StartDate, DateTimeKind.Utc);

            while (true)
            {
                var nextStart = DateTime.SpecifyKind(
                    WorkOrderRecurrence.ComputeNextExecution(cursorStart, template.RecurrenceInterval),
                    DateTimeKind.Utc);

                if (!IsWithinCalendarYear(nextStart, calendarYear))
                    break;

                if (existingStarts.Any(s => IsSamePeriod(s, nextStart)))
                {
                    cursorStart = nextStart;
                    continue;
                }

                var nextEnd = nextStart.Add(duration);
                var clone = CreatePeriodFromTemplate(template, nextStart, nextEnd);
                _context.WorkOrders.Add(clone);
                existingStarts.Add(nextStart);
                created++;
                cursorStart = nextStart;
            }

            template.NextExecutionDate = null;
            template.UpdatedAt = DateTime.UtcNow;

            if (created > 0)
            {
                _logger.LogInformation(
                    "Periyodik şablon {TemplateId}: {Count} dönem oluşturuldu (yıl {Year})",
                    template.Id, created, calendarYear);
            }

            return created;
        }

        public async Task<PeriodicBackfillResult> BackfillAllTemplatesAsync(CancellationToken cancellationToken = default)
        {
            var result = new PeriodicBackfillResult();
            var templates = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted && w.IsPeriodic && w.ParentWorkOrderId == null)
                .ToListAsync(cancellationToken);

            foreach (var template in templates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var beforeLabel = template.PeriodLabel;
                var created = await EnsureYearPeriodsAsync(template, cancellationToken);
                if (string.IsNullOrWhiteSpace(beforeLabel) && !string.IsNullOrWhiteSpace(template.PeriodLabel))
                    result.PeriodLabelsUpdated++;

                if (created > 0)
                {
                    result.TemplatesProcessed++;
                    result.PeriodsCreated += created;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return result;
        }

        internal static void EnsureTemplatePeriodLabel(WorkOrder template, int calendarYear)
        {
            if (!string.IsNullOrWhiteSpace(template.PeriodLabel))
                return;

            template.PeriodLabel = FormatPeriodLabel(template.StartDate, calendarYear);
        }

        internal static bool IsWithinCalendarYear(DateTime periodStartUtc, int calendarYear)
        {
            var local = TurkeyTime.ToLocal(periodStartUtc);
            return local.Year == calendarYear && local.Month >= 1 && local.Month <= 12;
        }

        internal static int GetLocalYear(DateTime utc)
        {
            return TurkeyTime.ToLocal(utc).Year;
        }

        internal static string FormatPeriodLabel(DateTime startUtc, int calendarYear)
        {
            var local = TurkeyTime.ToLocal(startUtc);
            if (local.Year == calendarYear)
                return local.ToString("yyyy-MM");

            return $"{calendarYear}-{local.Month:D2}";
        }

        internal static bool IsSamePeriod(DateTime aUtc, DateTime bUtc)
        {
            var a = TurkeyTime.ToLocal(aUtc);
            var b = TurkeyTime.ToLocal(bUtc);
            return a.Year == b.Year && a.Month == b.Month;
        }

        internal static WorkOrder CreatePeriodFromTemplate(WorkOrder template, DateTime start, DateTime end)
        {
            Point? location = null;
            if (template.Location != null)
            {
                location = new Point(template.Location.X, template.Location.Y)
                {
                    SRID = template.Location.SRID > 0 ? template.Location.SRID : 4326,
                };
            }

            var hasAssignee = template.AssignedToUserId.HasValue && template.AssignedToUserId != Guid.Empty;

            return new WorkOrder
            {
                Title = template.Title,
                CustomerName = template.CustomerName,
                Description = template.Description,
                MobileDescription = template.MobileDescription,
                Address = template.Address,
                Priority = template.Priority,
                WorkType = template.WorkType,
                WorkCategory = template.WorkCategory,
                StartDate = start,
                EndDate = end,
                Location = location!,
                OperationUserId = template.OperationUserId,
                OpenedByUserId = template.OpenedByUserId,
                AssignedToUserId = template.AssignedToUserId,
                TenantId = template.TenantId,
                CustomerId = template.CustomerId,
                CityId = template.CityId,
                DistrictId = template.DistrictId,
                IsPeriodic = false,
                RecurrenceInterval = "None",
                NextExecutionDate = null,
                Status = WorkOrderStatus.ResolveForCreate(hasAssignee),
                ParentWorkOrderId = template.Id,
                PeriodLabel = FormatPeriodLabel(start, GetLocalYear(start)),
            };
        }
    }
}
