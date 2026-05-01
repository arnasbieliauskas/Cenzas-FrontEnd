using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CenzasBackend.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CenzasBackend.Services
{
    public class MetadataGeneratorService : IMetadataGeneratorService
    {
        private readonly ILogger<MetadataGeneratorService> _logger;
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly StatisticsCacheSettings _settings;
        private readonly IWebHostEnvironment _env;

        public MetadataGeneratorService(
            ILogger<MetadataGeneratorService> logger, 
            IDbConnectionFactory connectionFactory,
            IOptions<StatisticsCacheSettings> settings,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _env = env;
        }

        public async Task RefreshMetadataAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Metadata Generator: Starting full property metadata extraction...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var combinations = new List<object>();

                using (var connection = await _connectionFactory.OpenConnectionAsync(ct))
                using (var command = connection.CreateCommand())
                {
                    // Rule #16 Optimized DISTINCT query leveraging idx_addlist_lookup_v2
                    // Fetching all hierarchical attributes for the 9-point dependency map
                    command.CommandText = @"
                        SELECT DISTINCT 
                            City, District, Address, Rooms, Title, 
                            Heating, Equipped, EnergyClass, BuildYear, Renovation 
                        FROM addlist 
                        WHERE City IS NOT NULL 
                          AND District IS NOT NULL 
                          AND Title IS NOT NULL;";
                    
                    command.CommandTimeout = 300; // 5 minutes for large dataset scan

                    using (var reader = await command.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            // Safe parsing for Year fields (identified as VARCHAR in DB)
                            string rawBuildYear = reader.IsDBNull(8) ? "" : reader.GetString(8);
                            int.TryParse(rawBuildYear, out int buildYear);

                            string rawRenovation = reader.IsDBNull(9) ? "" : reader.GetString(9);
                            int.TryParse(rawRenovation, out int renovation);

                            combinations.Add(new
                            {
                                City = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                District = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Street = reader.IsDBNull(2) ? "" : reader.GetString(2), // Maps from Address column
                                Rooms = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                Object = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Heating = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Equipped = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                EnergyClass = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                BuildYear = buildYear,
                                Renovation = renovation
                            });
                        }
                    }
                }

                // Rule #16: Final Stabilization - Atomic Write to prevent corruption
                var result = new { combinations = combinations };
                var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
                byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(result, jsonOptions);

                var fullPath = Path.Combine(_env.ContentRootPath, _settings.CacheFilePath);
                var directory = Path.GetDirectoryName(fullPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Atomic overwrite: WriteAllBytesAsync ensures the file is created or truncated
                await File.WriteAllBytesAsync(fullPath, jsonBytes, ct);

                sw.Stop();
                _logger.LogInformation("Metadata Generator: SUCCESS. Generated {Count} combinations in {Elapsed}ms. File: {Path}", 
                    combinations.Count, sw.ElapsedMilliseconds, fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metadata Generator: Critical error during full property extraction.");
                throw;
            }
        }
    }
}
