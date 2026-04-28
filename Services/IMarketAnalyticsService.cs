using System;
using System.Collections.Generic;

namespace CenzasBackend.Services
{
    public interface IMarketAnalyticsService
    {
        /// <summary>
        /// Calculates the market stability score (0-100) based on price variance over time.
        /// </summary>
        int CalculateStabilityScore(List<decimal> monthlyPrices);

        /// <summary>
        /// Calculates the Month-over-Month percentage change.
        /// </summary>
        decimal CalculateMonthlyChange(List<decimal> monthlyPrices);

        /// <summary>
        /// Determines the optimal SQL grouping and date display format based on the date range span.
        /// </summary>
        (string GroupingSql, string DateDisplaySql) DetermineAdaptiveGrouping(DateTime? minDate, DateTime? maxDate);
    }
}
