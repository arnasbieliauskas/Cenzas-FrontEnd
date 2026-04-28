using CenzasBackend.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenzasBackend.Services
{
    public interface IStatisticsService
    {
        Task<object> AnalyzeAsync(AnalysisRequest request, CancellationToken ct = default);
        Task<object> GetMarketTrendsAsync(AnalysisRequest request, CancellationToken ct = default);
        Task<object> GetListingsAsync(AnalysisRequest request, CancellationToken ct = default);
        Task UpdateCacheAsync(CancellationToken cancellationToken = default);
    }
}
