using CenzasBackend.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenzasBackend.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<StatisticsService> _logger;
        private readonly IMarketAnalyticsService _analyticsService;

        private static readonly string[] PriorityCities = { "Vilnius", "Kaunas", "Klaipėda", "Šiauliai", "Panevėžys", "Alytus", "Palanga" };

        public StatisticsService(
            IDbConnectionFactory connectionFactory, 
            ILogger<StatisticsService> logger,
            IMarketAnalyticsService analyticsService)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
            _analyticsService = analyticsService;
        }

        public async Task<object> AnalyzeAsync(AnalysisRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation("Analyzing market for city: {City}", request.City);

            using (var connection = await _connectionFactory.OpenConnectionAsync(ct))
            using (var command = connection.CreateCommand())
            {
                var (whereClause, parameters) = BuildWhereClause(request, command, "s");
                
                command.CommandText = $@"
                    SELECT 
                        AVG(s.Price) as AvgPrice,
                        AVG(s.Price / s.Area) as AvgPricePerM2,
                        COUNT(*) as TotalOffers,
                        MIN(s.Price) as MinPrice,
                        MAX(s.Price) as MaxPrice,
                        MIN(s.Area) as MinArea,
                        MAX(s.Area) as MaxArea
                    FROM analytics_snapshot s
                    {whereClause}";
                
                command.CommandTimeout = 120; // Rule #10

                using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    if (await reader.ReadAsync(ct))
                    {
                        var avgPrice = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                        var avgPricePerM2 = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                        var totalOffers = reader.GetInt64(2);

                        // For Stability Score, we might need more data, but for now we'll use a placeholder or 
                        // a secondary query if needed. Per request, we focus on the score calculation logic.
                        // Here we return the main metrics.
                        
                        return new
                        {
                            AvgPrice = Math.Round(avgPrice, 2),
                            AvgPricePerM2 = Math.Round(avgPricePerM2, 2),
                            TotalOffers = totalOffers,
                            MinPrice = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                            MaxPrice = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                            MinArea = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                            MaxArea = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                            MarketHealthIndex = 100 // Simplified for Analyze endpoint
                        };
                    }
                }
            }

            return new { TotalOffers = 0 };
        }

        public async Task<object> GetMarketTrendsAsync(AnalysisRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation("Fetching market trends for city: {City}", request.City);

            using (var connection = await _connectionFactory.OpenConnectionAsync(ct))
            using (var command = connection.CreateCommand())
            {
                var (whereClause, parameters) = BuildWhereClause(request, command, "a");

                // Discovery of min/max dates for adaptive grouping
                DateTime? minDate = null;
                DateTime? maxDate = null;
                
                if (!string.IsNullOrEmpty(request.DateFrom)) DateTime.TryParse(request.DateFrom, out var d1);
                if (!string.IsNullOrEmpty(request.DateTo)) DateTime.TryParse(request.DateTo, out var d2);

                // Use analytics service for adaptive grouping logic
                var (groupingSql, displaySql) = _analyticsService.DetermineAdaptiveGrouping(
                    DateTime.TryParse(request.DateFrom, out var start) ? start : null, 
                    DateTime.TryParse(request.DateTo, out var end) ? end : null);

                command.CommandText = $@"
                    SELECT 
                        {groupingSql} as DatePoint,
                        AVG(s.Price) as AveragePrice,
                        COUNT(DISTINCT a.ExternalId) as OfferCount
                    FROM analytics_snapshot a
                    JOIN secaddcollection s ON a.ExternalId = s.ExternalId
                    {whereClause}
                    AND s.Price > 0
                    GROUP BY DatePoint
                    HAVING AveragePrice > 0
                    ORDER BY DatePoint ASC
                    LIMIT 500";
                
                _logger.LogInformation("Trend Query SQL: {Sql}", command.CommandText);

                command.CommandTimeout = 120;

                var trendData = new List<object>();
                var priceList = new List<decimal>();

                using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var datePoint = reader.GetValue(0).ToString();
                        var avgPrice = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                        var offerCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);

                        if (avgPrice <= 0) continue; // Rule #11 / #12

                        trendData.Add(new { 
                            t = datePoint, 
                            v = Math.Round(avgPrice, 2),
                            c = offerCount
                        });
                        priceList.Add(avgPrice);
                    }
                }

                return new
                {
                    Trends = trendData,
                    StabilityScore = _analyticsService.CalculateStabilityScore(priceList),
                    MonthlyChange = _analyticsService.CalculateMonthlyChange(priceList)
                };
            }
        }

        public async Task<object> GetListingsAsync(AnalysisRequest request, CancellationToken ct = default)
        {
            _logger.LogInformation("Fetching listings for city: {City}", request.City);

            using (var connection = await _connectionFactory.OpenConnectionAsync(ct))
            using (var command = connection.CreateCommand())
            {
                var (whereClause, _) = BuildWhereClause(request, command, "a");

                // Count total for pagination
                command.CommandText = $"SELECT COUNT(*) FROM analytics_snapshot a {whereClause}";
                var totalCount = (long)await command.ExecuteScalarAsync(ct);

                // Fetch page 1 (Rule #24: High-speed snapshot retrieval)
                command.CommandText = $@"
                    SELECT 
                        a.Object, a.Price, a.Address, a.Area, a.Rooms, a.LatestPrice, a.LastCollectedDate, a.Url, a.ExternalId, a.InitialDate
                    FROM analytics_snapshot a
                    {whereClause}
                    ORDER BY a.LastCollectedDate DESC
                    LIMIT 25";

                var listings = new List<object>();
                using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        listings.Add(new
                        {
                            Object = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            InitialPrice = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1), 
                            Address = reader.IsDBNull(2) ? "" : reader.GetString(2), 
                            Area = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3),  
                            Rooms = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                            LatestPrice = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                            LatestDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6).ToString("yyyy-MM-dd"),
                            Url = reader.IsDBNull(7) ? "" : reader.GetString(7),
                            ExternalId = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            InitialDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9).ToString("yyyy-MM-dd")
                        });
                    }
                }

                return new
                {
                    Listings = listings,
                    TotalCount = totalCount
                };
            }
        }

        public async Task UpdateCacheAsync(CancellationToken cancellationToken = default)
        {
            // Implementation for background worker cache updates
            await Task.CompletedTask;
        }

        private (string WhereClause, List<DbParameter> Parameters) BuildWhereClause(AnalysisRequest request, IDbCommandWrapper command, string tableAlias = "a")
        {
            // Rule #15: Mandatory leading space to prevent aWHERE syntax errors
            var sb = new StringBuilder(" WHERE 1=1");
            var parameters = new List<DbParameter>();

            _logger.LogInformation("Building WHERE clause for request. DateFrom: {DateFrom}, DateTo: {DateTo}", request.DateFrom, request.DateTo);

            // City (Priority handling)
            if (!string.IsNullOrEmpty(request.City))
            {
                sb.Append($" AND {tableAlias}.City = @City");
                AddParameter(command, "@City", request.City.Trim().ToLower());
            }

            // Districts (Multi-select IN clause - Rule #15)
            if (request.Districts != null && request.Districts.Any())
            {
                var districtParams = new List<string>();
                for (int i = 0; i < request.Districts.Count; i++)
                {
                    var paramName = $"@Dist{i}";
                    districtParams.Add(paramName);
                    AddParameter(command, paramName, request.Districts[i]);
                }
                sb.Append($" AND {tableAlias}.District IN ({string.Join(",", districtParams)})");
            }

            // Rooms
            if (request.Rooms != null && request.Rooms.Any())
            {
                sb.Append($" AND {tableAlias}.Rooms IN ({string.Join(",", request.Rooms)})");
            }

            // Objects (Rule #22: snapshot always uses 'Object')
            if (request.Objects != null && request.Objects.Any())
            {
                var objectParams = new List<string>();
                for (int i = 0; i < request.Objects.Count; i++)
                {
                    var paramName = $"@Obj{i}";
                    objectParams.Add(paramName);
                    AddParameter(command, paramName, request.Objects[i].Trim().ToLower());
                }
                sb.Append($" AND {tableAlias}.Object IN ({string.Join(",", objectParams)})");
            }

            // Year Ranges (Rule #12)
            if (request.BuildYearFrom.HasValue)
            {
                sb.Append($" AND {tableAlias}.BuildYear >= @BuildYearFrom");
                AddParameter(command, "@BuildYearFrom", request.BuildYearFrom.Value);
            }
            if (request.BuildYearTo.HasValue)
            {
                sb.Append($" AND {tableAlias}.BuildYear <= @BuildYearTo");
                AddParameter(command, "@BuildYearTo", request.BuildYearTo.Value);
            }

            // Price Ranges
            if (request.PriceFrom.HasValue)
            {
                sb.Append($" AND {tableAlias}.Price >= @PriceFrom");
                AddParameter(command, "@PriceFrom", request.PriceFrom.Value);
            }
            if (request.PriceTo.HasValue)
            {
                sb.Append($" AND {tableAlias}.Price <= @PriceTo");
                AddParameter(command, "@PriceTo", request.PriceTo.Value);
            }

            // Area Ranges
            if (request.AreaFrom.HasValue)
            {
                sb.Append($" AND {tableAlias}.Area >= @AreaFrom");
                AddParameter(command, "@AreaFrom", (double)request.AreaFrom.Value);
            }
            if (request.AreaTo.HasValue)
            {
                sb.Append($" AND {tableAlias}.Area <= @AreaTo");
                AddParameter(command, "@AreaTo", (double)request.AreaTo.Value);
            }

            // Price Status (Rule #5: Only Price Drops / Price Hikes)
            if (!string.IsNullOrEmpty(request.PriceStatus) && request.PriceStatus != "all")
            {
                if (request.PriceStatus == "down") sb.Append($" AND {tableAlias}.LatestPrice < {tableAlias}.Price");
                else if (request.PriceStatus == "up") sb.Append($" AND {tableAlias}.LatestPrice > {tableAlias}.Price");
            }

            // Validity Status (Rule #5: Valid / Probable Expired)
            if (!string.IsNullOrEmpty(request.ValidityStatus) && request.ValidityStatus != "all")
            {
                var threshold = request.ExpiredThresholdDays ?? 1;
                var op = request.ValidityStatus == "valid" ? ">=" : "<";
                sb.Append($" AND {tableAlias}.LastCollectedDate {op} DATE_SUB(NOW(), INTERVAL @Threshold DAY)");
                AddParameter(command, "@Threshold", threshold);
            }

            // Date Ranges (Rule: Filter by Initial Registration Date)
            if (!string.IsNullOrEmpty(request.DateFrom) && DateTime.TryParse(request.DateFrom, out var dFrom))
            {
                sb.Append($" AND {tableAlias}.InitialDate >= @DateFrom");
                AddParameter(command, "@DateFrom", dFrom);
            }
            if (!string.IsNullOrEmpty(request.DateTo) && DateTime.TryParse(request.DateTo, out var dTo))
            {
                sb.Append($" AND {tableAlias}.InitialDate <= @DateTo");
                AddParameter(command, "@DateTo", dTo);
            }

            return (sb.ToString(), parameters);
        }

        private void AddParameter(IDbCommandWrapper command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            command.Parameters.Add(param);
        }
    }
}