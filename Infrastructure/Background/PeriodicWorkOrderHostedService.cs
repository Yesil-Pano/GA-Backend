using GA.Application.Features.WorkOrders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GA.Infrastructure.Background
{
    /// <summary>
    /// Periyodik iş emri şablonlarından süresi gelen işleri otomatik üretir.
    /// </summary>
    public class PeriodicWorkOrderHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PeriodicWorkOrdersOptions _options;
        private readonly ILogger<PeriodicWorkOrderHostedService> _logger;

        public PeriodicWorkOrderHostedService(
            IServiceScopeFactory scopeFactory,
            IOptions<PeriodicWorkOrdersOptions> options,
            ILogger<PeriodicWorkOrderHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("PeriodicWorkOrders kapalı (Enabled=false). Hosted service bekliyor.");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 1, 1440));
            _logger.LogInformation(
                "PeriodicWorkOrderHostedService başladı. Aralık: {Minutes} dk",
                interval.TotalMinutes);

            // Kısa gecikme: uygulama ayaklansın
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IPeriodicWorkOrderService>();
                    var result = await service.ProcessDueAsync(stoppingToken);

                    if (result.WorkOrdersCreated > 0)
                    {
                        _logger.LogInformation(
                            "Periyodik otomasyon: {Templates} şablon, {Created} yeni iş emri",
                            result.TemplatesProcessed, result.WorkOrdersCreated);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Periyodik iş emri otomasyonu hata verdi");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
