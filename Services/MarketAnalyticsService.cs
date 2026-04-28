using System;
using System.Collections.Generic;
using System.Linq;

namespace CenzasBackend.Services
{
    public class MarketAnalyticsService : IMarketAnalyticsService
    {
        /// <summary>
        /// Calculates the market stability score (0-100) based on price variance over time.
        /// Rule: 100 is peak stability. High variance reduces the score.
        /// </summary>
        public int CalculateStabilityScore(List<decimal> prices)
        {
            if (prices == null || prices.Count < 2) return 100;

            // Filter out anomalies (Zero prices)
            var validPrices = prices.Where(p => p > 0).Select(p => (double)p).ToList();
            if (validPrices.Count < 2) return 100;

            double avg = validPrices.Average();
            double sumOfSquares = validPrices.Sum(p => Math.Pow(p - avg, 2));
            double standardDeviation = Math.Sqrt(sumOfSquares / validPrices.Count);

            // Coefficient of Variation (CV) = SD / Mean
            double cv = standardDeviation / avg;

            // Score logic: 100% - (CV * 100). Cap at 0-100.
            // Example: If CV is 0.15 (15% variance), score is 85.
            int score = (int)Math.Max(0, Math.Min(100, 100 - (cv * 100)));
            
            return score;
        }

        public decimal CalculateMonthlyChange(List<decimal> prices)
        {
            if (prices == null || prices.Count < 2) return 0;
            
            var validPrices = prices.Where(p => p > 0).ToList();
            if (validPrices.Count < 2) return 0;

            decimal latest = validPrices.Last();
            decimal previous = validPrices[validPrices.Count - 2];

            if (previous == 0) return 0;

            return ((latest - previous) / previous) * 100;
        }

        /// <summary>
        /// Adaptive Grouping Logic per APP_LOGIC.md Section 4
        /// </summary>
        public (string GroupingSql, string DateDisplaySql) DetermineAdaptiveGrouping(DateTime? minDate, DateTime? maxDate)
        {
            if (!minDate.HasValue || !maxDate.HasValue) 
                return ("DATE(s.secdata)", "%Y-%m-%d");

            double totalDays = (maxDate.Value - minDate.Value).TotalDays;

            // Span > 730 days (2 years): Group by MONTH
            if (totalDays > 730)
            {
                return ("DATE_FORMAT(s.secdata, '%Y-%m-01')", "%Y-%m");
            }
            
            // Span > 180 days (6 months): Group by YEARWEEK
            if (totalDays > 180)
            {
                return ("STR_TO_DATE(CONCAT(YEARWEEK(s.secdata, 3), ' Monday'), '%X%V %W')", "%Y-%u");
            }

            // Default: Group by DATE
            return ("DATE(s.secdata)", "%Y-%m-%d");
        }
    }
}