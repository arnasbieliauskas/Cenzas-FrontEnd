using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;

namespace CenzasBackend.Services
{
    public class RpaJobWatcher : BackgroundService
    {
        private readonly ILogger<RpaJobWatcher> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDbConnectionFactory _connectionFactory;
        private int? _lastProcessedJobId;

        public RpaJobWatcher(
            ILogger<RpaJobWatcher> logger, 
            IConfiguration configuration, 
            IServiceScopeFactory scopeFactory,
            IDbConnectionFactory connectionFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _connectionFactory = connectionFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var checkIntervalSeconds = _configuration.GetValue<int>("RpaMonitorSettings:CheckIntervalSeconds", 60);
            var delayAfterDoneMinutes = _configuration.GetValue<int>("RpaMonitorSettings:DelayAfterDoneMinutes", 5);

            _logger.LogInformation("RPA Job Watcher: Background service starting using {ConnectionString}.", "WatcherConnection");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForJobsAsync(delayAfterDoneMinutes, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RPA Job Watcher: Error in monitoring loop.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CheckForJobsAsync(int delayAfterDoneMinutes, CancellationToken stoppingToken)
        {
            _logger.LogInformation("RPA Job Watcher: Checking for completed jobs...");

            using (var connection = await _connectionFactory.OpenConnectionAsync(stoppingToken))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, finished_at FROM rpa_job WHERE status = 'done' ORDER BY finished_at DESC LIMIT 1;";
                
                int? jobId = null;
                DateTime? finishedAt = null;

                using (var reader = await command.ExecuteReaderAsync(stoppingToken))
                {
                    if (await reader.ReadAsync(stoppingToken))
                    {
                        jobId = reader.GetInt32(0);
                        finishedAt = reader.GetDateTime(1);
                    }
                }

                if (jobId.HasValue && finishedAt.HasValue && jobId != _lastProcessedJobId)
                {
                    var timeSinceFinished = DateTime.Now - finishedAt.Value;
                    if (timeSinceFinished >= TimeSpan.FromMinutes(delayAfterDoneMinutes))
                    {
                        await RunChainReactionAsync(jobId.Value, stoppingToken);
                        _lastProcessedJobId = jobId;
                    }
                    else
                    {
                        var remaining = TimeSpan.FromMinutes(delayAfterDoneMinutes) - timeSinceFinished;
                        _logger.LogInformation("RPA Job Watcher: Found job {JobId}, but waiting for delay ({Minutes} min remaining).", 
                            jobId, remaining.TotalMinutes.ToString("F1"));
                    }
                }
            }
        }

        private async Task RunChainReactionAsync(int jobId, CancellationToken stoppingToken)
        {
            _logger.LogInformation("RPA Job Watcher: New job {JobId} detected. Starting chain reaction...", jobId);

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    // Step A: Maintenance
                    var maintenanceService = scope.ServiceProvider.GetRequiredService<DatabaseMaintenanceService>();
                    await maintenanceService.EnsureDatabaseSchemaAsync();
                    _logger.LogInformation("Watcher: Maintenance completed for job {JobId}.", jobId);

                    // Step B: Cache Update
                    var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();
                    await statisticsService.UpdateCacheAsync(stoppingToken);
                    _logger.LogInformation("Watcher: Statistics cache refreshed after DB maintenance.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RPA Job Watcher: Chain reaction failed for job {JobId}.", jobId);
            }
        }
    }
}
