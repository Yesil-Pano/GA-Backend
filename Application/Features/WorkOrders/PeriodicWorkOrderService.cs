using GA.Application.Features.Notifications;
using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace GA.Application.Features.WorkOrders
{
    public class PeriodicWorkOrdersOptions
    {
        public const string SectionName = "PeriodicWorkOrders";

        public bool Enabled { get; set; } = true;
        public int IntervalMinutes { get; set; } = 15;
        public int MaxCatchUpPerTemplate { get; set; } = 5;
    }

    public class PeriodicWorkOrderResult
    {
        public int TemplatesProcessed { get; set; }
        public int WorkOrdersCreated { get; set; }
    }

    public interface IPeriodicWorkOrderService
    {
        Task<PeriodicWorkOrderResult> ProcessDueAsync(CancellationToken cancellationToken = default);
    }

    public class PeriodicWorkOrderService : IPeriodicWorkOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly PeriodicWorkOrdersOptions _options;
        private readonly INotificationService _notificationService;
        private readonly ILogger<PeriodicWorkOrderService> _logger;

        public PeriodicWorkOrderService(
            ApplicationDbContext context,
            IOptions<PeriodicWorkOrdersOptions> options,
            INotificationService notificationService,
            ILogger<PeriodicWorkOrderService> logger)
        {
            _context = context;
            _options = options.Value;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<PeriodicWorkOrderResult> ProcessDueAsync(CancellationToken cancellationToken = default)
        {
            var result = new PeriodicWorkOrderResult();
            if (!_options.Enabled)
                return result;

            var now = DateTime.UtcNow;
            var maxCatchUp = Math.Clamp(_options.MaxCatchUpPerTemplate, 1, 20);
            var created = new List<WorkOrder>();

            var dueTemplates = await _context.WorkOrders
                .IgnoreQueryFilters()
                .Where(w => !w.IsDeleted
                            && w.IsPeriodic
                            && w.NextExecutionDate != null
                            && w.NextExecutionDate <= now)
                .OrderBy(w => w.NextExecutionDate)
                .ToListAsync(cancellationToken);

            if (dueTemplates.Count == 0)
                return result;

            foreach (var template in dueTemplates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var createdForTemplate = 0;
                var duration = WorkOrderRecurrence.ResolveDuration(template.StartDate, template.EndDate);

                while (template.NextExecutionDate.HasValue
                       && template.NextExecutionDate.Value <= now
                       && createdForTemplate < maxCatchUp)
                {
                    var cycleStart = DateTime.SpecifyKind(template.NextExecutionDate.Value, DateTimeKind.Utc);
                    var cycleEnd = cycleStart.Add(duration);

                    var clone = CreateExecutionFromTemplate(template, cycleStart, cycleEnd);
                    _context.WorkOrders.Add(clone);
                    created.Add(clone);

                    template.NextExecutionDate = WorkOrderRecurrence.ComputeNextExecution(
                        cycleStart, template.RecurrenceInterval);
                    template.UpdatedAt = DateTime.UtcNow;

                    createdForTemplate++;
                    result.WorkOrdersCreated++;
                }

                if (createdForTemplate > 0)
                {
                    result.TemplatesProcessed++;
                    _logger.LogInformation(
                        "Periyodik şablon {TemplateId} için {Count} iş emri üretildi. Sonraki: {Next}",
                        template.Id, createdForTemplate, template.NextExecutionDate);
                }
            }

            if (result.WorkOrdersCreated > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                foreach (var wo in created)
                {
                    await _notificationService.NotifyAsync(
                        "WorkOrderPeriodic",
                        "Periyodik iş emri açıldı",
                        $"{wo.CustomerName}: {wo.Title}",
                        wo.TenantId,
                        wo.Id,
                        null,
                        wo.AssignedToUserId,
                        cancellationToken);
                }
            }

            return result;
        }

        private static WorkOrder CreateExecutionFromTemplate(WorkOrder template, DateTime start, DateTime end)
        {
            Point? location = null;
            if (template.Location != null)
            {
                location = new Point(template.Location.X, template.Location.Y)
                {
                    SRID = template.Location.SRID > 0 ? template.Location.SRID : 4326
                };
            }

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
                Status = "Bekliyor",
            };
        }
    }
}
