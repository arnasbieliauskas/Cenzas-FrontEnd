using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Data.Common;

namespace CenzasBackend.Services
{
    public class DiagnosticService
    {
        private readonly ILogger<DiagnosticService> _logger;
        private readonly IDbConnectionFactory _connectionFactory;

        public DiagnosticService(ILogger<DiagnosticService> logger, IDbConnectionFactory connectionFactory)
        {
            _logger = logger;
            _connectionFactory = connectionFactory;
        }

        public async Task RunDiagnosticsAsync()
        {
            _logger.LogInformation("====================================================================");
            _logger.LogInformation("STAGE 23.3: MYSQL CONNECTIVITY & INTEGRITY DIAGNOSTICS");
            _logger.LogInformation("====================================================================");

            try
            {
                // 1. Connection Status
                using (var connection = await _connectionFactory.OpenConnectionAsync())
                {
                    _logger.LogInformation("[OK] Connection Status: Handshake successful with {Host}", connection.DataSource);
                }

                // 2. Database Schema Audit (MySQL version)
                _logger.LogInformation("--- Database Schema Audit ---");
                using (var connection = await _connectionFactory.OpenConnectionAsync())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE();";
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var tables = new List<string>();
                        while (await reader.ReadAsync())
                        {
                            tables.Add(reader.GetString(0));
                        }
                        _logger.LogInformation("Found Tables: {Tables}", string.Join(", ", tables));

                        string[] expectedTables = { "addlist", "secaddcollection", "agent", "house_details", "rpa_job", "system_documentation" };
                        foreach (var expected in expectedTables)
                        {
                            if (tables.Contains(expected))
                                _logger.LogInformation("[OK] Table '{Table}' exists.", expected);
                            else
                                _logger.LogWarning("[MISSING] Table '{Table}' was not found in MySQL database!", expected);
                        }
                    }
                }

                // 3. Row Count Validation
                _logger.LogInformation("--- Row Count Validation ---");
                await LogRowCountAsync("secaddcollection", 993225);
                await LogRowCountAsync("addlist", 38612);
                await LogRowCountAsync("rpa_job", 549);

                // 4. Data Peek (Mojibake Check)
                _logger.LogInformation("--- Data Peek (First 2 rows from addlist) ---");
                using (var connection = await _connectionFactory.OpenConnectionAsync())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT title, price, externalid FROM addlist LIMIT 2;";
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        int rowNum = 1;
                        while (await reader.ReadAsync())
                        {
                            var title = reader.IsDBNull(0) ? "N/A" : reader.GetString(0);
                            var price = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                            var extId = reader.IsDBNull(2) ? "N/A" : reader.GetValue(2).ToString();
                            _logger.LogInformation("Row {Num}: Title='{Title}', Price={Price}, ExternalId='{ExtId}'", rowNum++, title, price, extId);
                        }
                    }
                }

                _logger.LogInformation("====================================================================");
                _logger.LogInformation("DIAGNOSTICS SEQUENCE COMPLETED");
                _logger.LogInformation("====================================================================");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "CRITICAL ERROR during MySQL diagnostics!");
                throw;
            }
        }

        private async Task LogRowCountAsync(string tableName, int expected)
        {
            using (var connection = await _connectionFactory.OpenConnectionAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT COUNT(*) FROM `{tableName}`;";
                var count = Convert.ToInt64(await command.ExecuteScalarAsync());
                _logger.LogInformation("Table '{Table}': Count={Count} (Expected: ~{Expected})", tableName, count, expected);
            }
        }
    }
}