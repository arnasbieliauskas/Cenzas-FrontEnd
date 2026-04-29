using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CenzasBackend.Services
{
    public class RpaJobWatcher : BackgroundService
    {
        private readonly ILogger<RpaJobWatcher> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDbConnectionFactory _connectionFactory;
        private long _lastProcessedJobId = 0;

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
            
            _logger.LogInformation("RPA Job Watcher: Starting monitoring loop (Interval: {Interval}s).", checkIntervalSeconds);

            // Initialize last processed ID to current max to avoid processing old jobs on startup
            await InitializeLastJobIdAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollForJobsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RPA Job Watcher: Critical error in monitoring loop.");
                }

                await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), stoppingToken);
            }
        }

        private async Task InitializeLastJobIdAsync(CancellationToken ct)
        {
            try
            {
                using (var connection = await OpenWithRetryAsync(ct))
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT MAX(id) FROM rpa_job WHERE status = 'done';";
                    var result = await command.ExecuteScalarAsync(ct);
                    if (result != null && result != DBNull.Value)
                    {
                        _lastProcessedJobId = Convert.ToInt64(result);
                        _logger.LogInformation("RPA Job Watcher: Initialized with LastProcessedJobId = {Id}", _lastProcessedJobId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RPA Job Watcher: Could not initialize LastJobId. Starting from 0.");
            }
        }

        private async Task PollForJobsAsync(CancellationToken ct)
        {
            _logger.LogDebug("RPA Job Watcher: Polling for new completed jobs (Last ID: {LastId})...", _lastProcessedJobId);

            using (var connection = await OpenWithRetryAsync(ct))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id FROM rpa_job WHERE status = 'done' AND id > @lastId ORDER BY id DESC LIMIT 1;";
                var param = command.CreateParameter();
                param.ParameterName = "@lastId";
                param.Value = _lastProcessedJobId;
                command.Parameters.Add(param);

                var result = await command.ExecuteScalarAsync(ct);
                if (result != null && result != DBNull.Value)
                {
                    long newJobId = Convert.ToInt64(result);
                    
                    var delayMinutes = _configuration.GetValue<int>("RpaMonitorSettings:DelayAfterDoneMinutes", 3);
                    if (delayMinutes > 0)
                    {
                        _logger.LogInformation("RPA Job Watcher: Found job {JobId}. Waiting {Minutes} minutes before processing...", newJobId, delayMinutes);
                        await Task.Delay(TimeSpan.FromMinutes(delayMinutes), ct);
                    }

                    await ExecuteChainReactionAsync(newJobId, ct);
                    _lastProcessedJobId = newJobId;
                }
            }
        }

        private async Task ExecuteChainReactionAsync(long jobId, CancellationToken ct)
        {
            _logger.LogInformation("RPA Job Watcher: New job {JobId} detected. Starting chain reaction...", jobId);

            using (var scope = _scopeFactory.CreateScope())
            {
                try
                {
                    // Step A: Update LastCollectedDate
                    await UpdateLastCollectedDatesAsync(ct);
                    _logger.LogInformation("Step A: LastCollectedDate synchronization completed.");

                    // Step B: Trigger Database Maintenance
                    var maintenanceService = scope.ServiceProvider.GetRequiredService<DatabaseMaintenanceService>();
                    await maintenanceService.PerformMaintenanceAsync();
                    _logger.LogInformation("Step B: Database maintenance completed.");

                    // Step C: Trigger Metadata Refresh
                    var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataGeneratorService>();
                    await metadataService.RefreshMetadataAsync(ct);
                    _logger.LogInformation("Step C: Metadata refresh completed.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RPA Job Watcher: Chain reaction failed for job {JobId}.", jobId);
                }
            }
        }

        private async Task UpdateLastCollectedDatesAsync(CancellationToken ct)
        {
            using (var connection = await OpenWithRetryAsync(ct))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    UPDATE addlist a
                    JOIN (
                        SELECT ExternalId, MAX(secdata) as LatestDate
                        FROM secaddcollection
                        GROUP BY ExternalId
                    ) s ON a.ExternalId = s.ExternalId
                    SET a.LastCollectedDate = s.LatestDate;";
                
                command.CommandTimeout = 300; // 5 minutes
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        private async Task<IDbConnectionWrapper> OpenWithRetryAsync(CancellationToken ct)
        {
            var start = DateTime.UtcNow;
            var retryCount = 0;

            while (DateTime.UtcNow - start < TimeSpan.FromSeconds(30))
            {
                try
                {
                    return await _connectionFactory.OpenConnectionAsync(ct);
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "RPA Job Watcher: DB connection failed (Attempt {Count}). Retrying...", retryCount);
                    await Task.Delay(2000, ct); // Wait 2s before retry
                }
            }

            throw new TimeoutException("RPA Job Watcher: Failed to open DB connection within 30s retry policy.");
        }
    }
}

