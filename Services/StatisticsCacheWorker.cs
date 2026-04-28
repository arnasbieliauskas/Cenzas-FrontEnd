using CenzasBackend.Models;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CenzasBackend.Services
{
    public class StatisticsCacheWorker : BackgroundService
    {
        private readonly ILogger<StatisticsCacheWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly StatisticsCacheSettings _settings;

        public StatisticsCacheWorker(
            ILogger<StatisticsCacheWorker> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<StatisticsCacheSettings> settings)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Statistics Cache Worker started. Update interval: {Interval} hours.", _settings.UpdateIntervalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();
                        await statisticsService.UpdateCacheAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating statistics cache.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(_settings.UpdateIntervalHours), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
