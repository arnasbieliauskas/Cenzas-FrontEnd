using System;
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
        private readonly BusinessLogicSettings _businessSettings;
        private readonly IWebHostEnvironment _env;

        public MetadataGeneratorService(
            ILogger<MetadataGeneratorService> logger, 
            IDbConnectionFactory connectionFactory,
            IOptions<StatisticsCacheSettings> settings,
            IOptions<BusinessLogicSettings> businessSettings,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _connectionFactory = connectionFactory;
            _settings = settings.Value;
            _businessSettings = businessSettings.Value;
            _env = env;
        }

        public async Task RefreshMetadataAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Metadata Generator: Starting property metadata extraction with Price Tracking...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var combinations = new List<object>();

                using (var connection = await _connectionFactory.OpenConnectionAsync(ct))
                using (var command = connection.CreateCommand())
                {
                    // Integruota patikrinta SQL logika: Initial vs Latest kainos/datos + Rule #24 saugikliai
                    command.CommandText = @"
                        SELECT 
                            a.City, a.District, a.Address AS Street, a.Rooms, a.Title AS Object, 
                            a.Heating, a.Equipped, a.EnergyClass, a.BuildYear, a.Renovation,
                            a.Price AS InitialPrice,
                            (SELECT MIN(s_min.secdata) FROM ntd_db_remote.secaddcollection s_min WHERE s_min.ExternalId = a.ExternalId) AS InitialDate,
                            latest.Price AS LatestPrice,
                            latest.secdata AS LatestDate,
                            a.Area,
                            a.Url
                        FROM ntd_db_remote.addlist a
                        LEFT JOIN (
                            SELECT s1.ExternalId, s1.Price, s1.secdata
                            FROM ntd_db_remote.secaddcollection s1
                            WHERE s1.secdata = (
                                SELECT MAX(s2.secdata) 
                                FROM ntd_db_remote.secaddcollection s2 
                                WHERE s2.ExternalId = s1.ExternalId
                            )
                        ) AS latest ON a.ExternalId = latest.ExternalId
                        WHERE a.City IS NOT NULL 
                          AND a.Title IS NOT NULL
                          AND a.Address NOT LIKE '%€%' 
                          AND a.Address NOT REGEXP '^[0-9[[:space:]]]+$';";
                    
                    command.CommandTimeout = 300; // 5 minučių limitas dideliems duomenų kiekiams

                    using (var reader = await command.ExecuteReaderAsync(ct))
                    {
                        while (await reader.ReadAsync(ct))
                        {
                            // Saugių metų reikšmių apdorojimas
                            string rawBuildYear = reader.IsDBNull(8) ? "0" : reader.GetString(8).ToString();
                            int.TryParse(rawBuildYear, out int buildYear);

                            string rawRenovation = reader.IsDBNull(9) ? "0" : reader.GetString(9).ToString();
                            int.TryParse(rawRenovation, out int renovation);

                            combinations.Add(new
                            {
                                City = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                District = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Street = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Rooms = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                Object = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Heating = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Equipped = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                EnergyClass = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                BuildYear = buildYear,
                                Renovation = renovation,
                                
                                // Nauji laukai kainų sekimui
                                InitialPrice = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                                InitialDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11).ToString("yyyy-MM-dd"),
                                LatestPrice = reader.IsDBNull(12) ? 0 : reader.GetDecimal(12),
                                LatestDate = reader.IsDBNull(13) ? null : reader.GetDateTime(13).ToString("yyyy-MM-dd"),
                                Area = reader.IsDBNull(14) ? 0 : reader.GetDecimal(14),
                                Url = reader.IsDBNull(15) ? "" : reader.GetString(15)
                            });
                        }
                    }
                }

                // Rule #16: Atomizuotas JSON įrašymas
                var result = new 
                { 
                    ListingExpirationDays = _businessSettings.ListingExpirationDays,
                    combinations = combinations 
                };
                var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
                byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(result, jsonOptions);

                var fullPath = Path.Combine(_env.ContentRootPath, _settings.CacheFilePath);
                var directory = Path.GetDirectoryName(fullPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Perrašome failą užtikrinant duomenų vientisumą
                await File.WriteAllBytesAsync(fullPath, jsonBytes, ct);

                sw.Stop();
                _logger.LogInformation("Metadata Generator: SUCCESS. Generated {Count} combinations in {Elapsed}ms. File: {Path}", 
                    combinations.Count, sw.ElapsedMilliseconds, fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metadata Generator: Critical error during property metadata extraction.");
                throw;
            }
        }
    }
}