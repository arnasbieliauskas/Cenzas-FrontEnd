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

            try
            {
                using (var connection = await _connectionFactory.OpenConnectionAsync())
                {

                    // 1. Column Maintenance (addlist)
                    await EnsureColumnExistsAsync(connection, "addlist", "LastCollectedDate", "DATETIME DEFAULT CURRENT_TIMESTAMP");

                    // 2. Strategic Indexes
                    // Table: addlist
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

                    // Table: secaddcollection
                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_external_date", "(ExternalId, secdata DESC)");
                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_price", "(Price)");
                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_date_only", "(secdata)");
                    await EnsureIndexExistsAsync(connection, "secaddcollection", "idx_secadd_date_price", "(secdata, Price)");

                    // 3. Table Optimization
                    _logger.LogInformation("Database Maintenance Guard: Optimizing tables...");
                    await ExecuteAsync(connection, "OPTIMIZE TABLE addlist;");
                    await ExecuteAsync(connection, "OPTIMIZE TABLE secaddcollection;");

                    // 4. Data Synchronization
                    await SynchronizeLastCollectedDatesAsync(connection);

                    // 5. Analytics Snapshot (Rule #2)
                    await EnsureAnalyticsSnapshotAsync(connection);
                }
                _logger.LogInformation("Database Maintenance Guard: Maintenance sequence completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database Maintenance Guard: Critical error during schema maintenance.");
            }
        }

        private async Task EnsureAnalyticsSnapshotAsync(IDbConnectionWrapper connection)
        {
            _logger.LogInformation("Database Maintenance Guard: Ensuring analytics_snapshot table...");
            await ExecuteAsync(connection, "DROP TABLE IF EXISTS analytics_snapshot;");
            string createSql = @"
                CREATE TABLE IF NOT EXISTS analytics_snapshot (
                    id INT PRIMARY KEY AUTO_INCREMENT,
                    City VARCHAR(100),
                    District VARCHAR(100),
                    Address VARCHAR(255),
                    Rooms INT,
                    Object VARCHAR(255),
                    Price DECIMAL(15,2),
                    Area DECIMAL(10,2),
                    BuildYear INT,
                    Renovation INT,
                    Heating VARCHAR(255),
                    Equipped VARCHAR(255),
                    EnergyClass VARCHAR(50),
                    INDEX idx_snapshot_lookup (City, District, Rooms, Object)
                ) ENGINE=InnoDB;";
            
            await ExecuteAsync(connection, createSql);

            _logger.LogInformation("Database Maintenance Guard: Refreshing analytics_snapshot data...");
            await ExecuteAsync(connection, "TRUNCATE TABLE analytics_snapshot;");
            
            // Population logic (Rule #2 & Rule #22)
            // Note: Safely handling the varchar-to-int conversion during the snapshot insert
            string populateSql = @"
                INSERT INTO analytics_snapshot (City, District, Address, Rooms, Object, Price, Area, BuildYear, Renovation, Heating, Equipped, EnergyClass)
                SELECT 
                    City, District, Address, Rooms, Title, Price, Area, 
                    CASE WHEN BuildYear REGEXP '^[0-9]+$' THEN CAST(BuildYear AS UNSIGNED) ELSE 0 END,
                    CASE WHEN Renovation REGEXP '^[0-9]+$' THEN CAST(Renovation AS UNSIGNED) ELSE 0 END,
                    Heating, Equipped, EnergyClass
                FROM addlist
                WHERE Price > 0 AND Area > 0;";
            
            await ExecuteAsync(connection, populateSql);
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

        private async Task ExecuteAsync(IDbConnectionWrapper connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandTimeout = 600; // 10 minutes for slow optimizations/indexes
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
