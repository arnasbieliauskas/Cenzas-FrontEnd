using CenzasBackend.Models;
using CenzasBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace CenzasBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ILogger<StatisticsController> _logger;

        public StatisticsController(IStatisticsService statisticsService, ILogger<StatisticsController> logger)
        {
            _statisticsService = statisticsService;
            _logger = logger;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze([FromBody] AnalysisRequest request)
        {
            if (request == null) return BadRequest();
            _logger.LogInformation("API: Analyze request received for city: {City}", request.City);
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await _statisticsService.AnalyzeAsync(request, HttpContext.RequestAborted);
            sw.Stop();

            _logger.LogInformation("API: Analyze completed for {City} in {Elapsed}ms", request.City, sw.ElapsedMilliseconds);
            return Ok(results);
        }

        [HttpPost("market-trend")]
        public async Task<IActionResult> GetMarketTrends([FromBody] AnalysisRequest request)
        {
            if (request == null) return BadRequest();
            _logger.LogInformation("API: MarketTrend request received for city: {City}", request.City);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await _statisticsService.GetMarketTrendsAsync(request, HttpContext.RequestAborted);
            sw.Stop();

            _logger.LogInformation("API: MarketTrend completed for {City} in {Elapsed}ms", request.City, sw.ElapsedMilliseconds);
            return Ok(results);
        }

        [HttpPost("listings")]
        public async Task<IActionResult> GetListings([FromBody] AnalysisRequest request)
        {
            if (request == null) return BadRequest();
            _logger.LogInformation("API: Listings request received for city: {City}", request.City);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = await _statisticsService.GetListingsAsync(request, HttpContext.RequestAborted);
            sw.Stop();

            _logger.LogInformation("API: Listings completed for {City} in {Elapsed}ms", request.City, sw.ElapsedMilliseconds);
            return Ok(results);
        }

        [HttpGet("health")]
        public IActionResult GetHealth() => Ok(new { status = "Healthy" });
    }
}
