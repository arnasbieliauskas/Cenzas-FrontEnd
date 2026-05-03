using MySqlConnector;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.Common;

namespace CenzasBackend.Services
{
    public class DatabaseMaintenanceService
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<DatabaseMaintenanceService> _logger;

        public DatabaseMaintenanceService(IDbConnectionFactory connectionFactory, ILogger<DatabaseMaintenanceService> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public virtual async Task PerformMaintenanceAsync()
        {
            _logger.LogInformation("Database Maintenance: Starting comprehensive maintenance sequence...");
            try
            {
                await EnsureDatabaseSchemaAsync();
                await CleanupOldRecordsAsync();
                _logger.LogInformation("Database Maintenance: All maintenance tasks completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database Maintenance: Maintenance sequence failed.");
            }
        }

        public virtual async Task CleanupOldRecordsAsync()
        {
            _logger.LogInformation("Database Maintenance: Cleaning up expired records...");
            try
            {
                using (var connection = await _connectionFactory.OpenConnectionAsync())
                {
                    // Cleanup rpa_job records older than 30 days
                    string sql = "DELETE FROM rpa_job WHERE created_at < DATE_SUB(NOW(), INTERVAL 30 DAY);";
                    await ExecuteAsync(connection, sql);
                }
                _logger.LogInformation("Database Maintenance: Cleanup completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database Maintenance: Cleanup failed.");
            }
        }

        public virtual async Task EnsureDatabaseSchemaAsync()
        {
            _logger.LogInformation("Database Maintenance Guard: Starting schema validation...");
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using (var connection = await _connectionFactory.OpenConnectionAsync())
                {
                    var sectionSw = System.Diagnostics.Stopwatch.StartNew();

                    // 1. Column Maintenance (addlist)
                    await EnsureColumnExistsAsync(connection, "addlist", "LastCollectedDate", "DATETIME DEFAULT CURRENT_TIMESTAMP");
                    _logger.LogInformation("Database Maintenance: Column validation took {Elapsed}ms.", sectionSw.ElapsedMilliseconds);
                    sectionSw.Restart();

                    // 2. Strategic Indexes
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_addlist_external", "(ExternalId)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_last_collected", "(LastCollectedDate)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_city_district", "(City, District)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_rooms", "(Rooms)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_addlist_city_buildyear", "(City, buildyear)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_addlist_city_renovation", "(City, renovation)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_addlist_city_energy", "(City, energyclass)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_addlist_heating", "(heating)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_addlist_equipped", "(equipped)");
                    await EnsureIndexExistsAsync(connection, "addlist", "idx_addlist_lookup_v2", "(City(50), District(50), Address(100), Rooms, Title(50))");

                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_external_date", "(ExternalId, secdata DESC)");
                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_price", "(Price)");
                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_date_only", "(secdata)");
                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_date_price", "(secdata, Price)");
                    
                    _logger.LogInformation("Database Maintenance: Index validation took {Elapsed}ms.", sectionSw.ElapsedMilliseconds);
                    sectionSw.Restart();

                    // 3. Table Optimization
                    _logger.LogInformation("Database Maintenance Guard: Optimizing tables...");
                    await ExecuteAsync(connection, "OPTIMIZE TABLE addlist;");
                    await ExecuteAsync(connection, "OPTIMIZE TABLE secaddcollection;");
                    _logger.LogInformation("Database Maintenance: Table optimization took {Elapsed}ms.", sectionSw.ElapsedMilliseconds);
                    sectionSw.Restart();

                    // 4. Data Synchronization
                    await SynchronizeLastCollectedDatesAsync(connection);
                    _logger.LogInformation("Database Maintenance: Date synchronization took {Elapsed}ms.", sectionSw.ElapsedMilliseconds);
                    sectionSw.Restart();

                    // 5. Analytics Snapshot
                    await EnsureAnalyticsSnapshotAsync(connection);
                    _logger.LogInformation("Database Maintenance: Snapshot generation took {Elapsed}ms.", sectionSw.ElapsedMilliseconds);
                }
                _logger.LogInformation("Database Maintenance Guard: Maintenance sequence completed successfully in {TotalElapsed}ms.", totalSw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database Maintenance Guard: Critical error during schema maintenance.");
            }
        }

        private async Task EnsureAnalyticsSnapshotAsync(IDbConnectionWrapper connection)
        {
            _logger.LogInformation("Database Maintenance Guard: Ensuring analytics_snapshot table (Rule #25)...");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Step A: Create optimized temporary table with Primary Key (Rule #25)
                _logger.LogInformation("Database Maintenance Guard: Pre-calculating price metadata via Temporary Table...");
                await ExecuteAsync(connection, "DROP TEMPORARY TABLE IF EXISTS temp_latest_prices;");
                await ExecuteAsync(connection, @"
                    CREATE TEMPORARY TABLE temp_latest_prices (
                        ExternalId VARCHAR(255) PRIMARY KEY,
                        LatestPrice DECIMAL(15,2),
                        LatestDate DATE,
                        InitialDate DATE,
                        INDEX idx_temp_ext (ExternalId)
                    ) AS
                    SELECT 
                        s1.ExternalId, 
                        s1.Price as LatestPrice, 
                        s1.secdata as LatestDate,
                        s_min.min_date as InitialDate
                    FROM secaddcollection s1
                    INNER JOIN (
                        SELECT ExternalId, MAX(secdata) as max_date
                        FROM secaddcollection
                        GROUP BY ExternalId
                    ) s2 ON s1.ExternalId = s2.ExternalId AND s1.secdata = s2.max_date
                    LEFT JOIN (
                        SELECT ExternalId, MIN(secdata) as min_date
                        FROM secaddcollection
                        GROUP BY ExternalId
                    ) s_min ON s1.ExternalId = s_min.ExternalId;", 600);
                
                _logger.LogInformation("Database Maintenance: Price metadata pre-calculated in {Elapsed}ms.", sw.ElapsedMilliseconds);
                sw.Restart();

                // Step B: Rebuild the analytics_snapshot table structure (Rule #25)
                _logger.LogInformation("Database Maintenance Guard: Rebuilding analytics_snapshot structure...");
                await ExecuteAsync(connection, "DROP TABLE IF EXISTS analytics_snapshot;");
                string createSql = @"
                    CREATE TABLE analytics_snapshot (
                        id INT PRIMARY KEY AUTO_INCREMENT,
                        ExternalId VARCHAR(255),
                        City VARCHAR(100), District VARCHAR(100), Address VARCHAR(255),
                        Rooms INT, Object VARCHAR(255), Price DECIMAL(15,2),
                        LatestPrice DECIMAL(15,2), InitialDate DATETIME, LatestDate DATETIME,
                        Area DECIMAL(10,2), BuildYear INT, Renovation INT, Heating VARCHAR(255),
                        Equipped VARCHAR(255), EnergyClass VARCHAR(50),
                        Url VARCHAR(500), LastCollectedDate DATETIME,
                        INDEX idx_snapshot_external (ExternalId),
                        INDEX idx_snapshot_lookup (City, District, Rooms, Object)
                    ) ENGINE=InnoDB;";
                await ExecuteAsync(connection, createSql);
                _logger.LogInformation("Database Maintenance: Snapshot structure rebuilt in {Elapsed}ms.", sw.ElapsedMilliseconds);
                sw.Restart();

                // Step C: Fast Insert using Indexed Join (Rule #25)
                _logger.LogInformation("Database Maintenance Guard: Performing Fast Insert into snapshot...");
                string populateSql = @"
                    INSERT INTO analytics_snapshot (ExternalId, City, District, Address, Rooms, Object, Price, LatestPrice, InitialDate, LatestDate, Area, BuildYear, Renovation, Heating, Equipped, EnergyClass, Url, LastCollectedDate)
                    SELECT 
                        a.ExternalId, a.City, a.District, a.Address, a.Rooms, a.Title, 
                        a.Price, COALESCE(t.LatestPrice, a.Price), 
                        COALESCE(t.InitialDate, a.LastCollectedDate),
                        COALESCE(t.LatestDate, a.LastCollectedDate),
                        a.Area,
                        CAST(CASE WHEN a.BuildYear REGEXP '^[0-9]+$' THEN a.BuildYear ELSE 0 END AS UNSIGNED),
                        CAST(CASE WHEN a.Renovation REGEXP '^[0-9]+$' THEN a.Renovation ELSE 0 END AS UNSIGNED),
                        a.Heating, a.Equipped, a.EnergyClass, a.Url, a.LastCollectedDate
                    FROM addlist a
                    LEFT JOIN temp_latest_prices t ON a.ExternalId = t.ExternalId
                    WHERE a.Price > 0 AND a.Area > 0;";
                
                await ExecuteAsync(connection, populateSql, 1200); // 20 minutes timeout as requested
                _logger.LogInformation("Database Maintenance: Snapshot population completed in {Elapsed}ms.", sw.ElapsedMilliseconds);
            }
            finally
            {
                // Step D: Cleanup (Rule #25)
                try { await ExecuteAsync(connection, "DROP TEMPORARY TABLE IF EXISTS temp_latest_prices;"); } catch { }
            }
        }

        private async Task EnsureColumnExistsAsync(IDbConnectionWrapper connection, string tableName, string columnName, string typeDefinition)
        {
            var exists = false;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @table AND column_name = @column;";
                
                var pTable = command.CreateParameter();
                pTable.ParameterName = "@table";
                pTable.Value = tableName;
                command.Parameters.Add(pTable);

                var pColumn = command.CreateParameter();
                pColumn.ParameterName = "@column";
                pColumn.Value = columnName;
                command.Parameters.Add(pColumn);

                exists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            }

            if (!exists)
            {
                _logger.LogInformation("Database Maintenance Guard: Adding column {Column} to {Table}...", columnName, tableName);
                await ExecuteAsync(connection, $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {typeDefinition};");
            }
            else
            {
                _logger.LogInformation("Database Maintenance Guard: Column {Column} already exists in {Table}.", columnName, tableName);
            }
        }

        private async Task EnsureIndexExistsAsync(IDbConnectionWrapper connection, string tableName, string indexName, string columnsDefinition)
        {
            try
            {
                var exists = false;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = @table AND index_name = @index;";
                    
                    var pTable = command.CreateParameter();
                    pTable.ParameterName = "@table";
                    pTable.Value = tableName;
                    command.Parameters.Add(pTable);

                    var pIndex = command.CreateParameter();
                    pIndex.ParameterName = "@index";
                    pIndex.Value = indexName;
                    command.Parameters.Add(pIndex);

                    exists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }

                if (!exists)
                {
                    _logger.LogInformation("Database Maintenance Guard: Creating index {Index} on {Table}...", indexName, tableName);
                    await ExecuteAsync(connection, $"CREATE INDEX `{indexName}` ON `{tableName}` {columnsDefinition};");
                }
                else
                {
                    _logger.LogInformation("Database Maintenance Guard: Index {Index} already exists on {Table}.", indexName, tableName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database Maintenance Guard: Non-fatal error while ensuring index {Index} on {Table}.", indexName, tableName);
            }
        }

        private async Task ExecuteAsync(IDbConnectionWrapper connection, string sql, int timeoutSeconds = 600)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandTimeout = timeoutSeconds; 
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task SynchronizeLastCollectedDatesAsync(IDbConnectionWrapper connection)
        {
            _logger.LogInformation("Database Maintenance Guard: Synchronizing LastCollectedDate from history...");
            try
            {
                string sql = @"
                    UPDATE addlist a
                    JOIN (
                        SELECT ExternalId, MAX(secdata) as LatestDate
                        FROM secaddcollection
                        GROUP BY ExternalId
                    ) s ON a.ExternalId = s.ExternalId
                    SET a.LastCollectedDate = s.LatestDate;";

                await ExecuteAsync(connection, sql);
                _logger.LogInformation("Maintenance: Successfully synchronized LastCollectedDate from history.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database Maintenance Guard: Failed to synchronize LastCollectedDate.");
            }
        }
    }
}
