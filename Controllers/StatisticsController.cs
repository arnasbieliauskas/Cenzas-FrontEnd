using CenzasBackend.Models;
using CenzasBackend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CenzasBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze([FromBody] AnalysisRequest request)
        {
            if (request == null) return BadRequest();
            var results = await _statisticsService.AnalyzeAsync(request, HttpContext.RequestAborted);
            return Ok(results);
        }

        [HttpPost("market-trend")]
        public async Task<IActionResult> GetMarketTrends([FromBody] AnalysisRequest request)
        {
            if (request == null) return BadRequest();
            var results = await _statisticsService.GetMarketTrendsAsync(request, HttpContext.RequestAborted);
            return Ok(results);
        }

        [HttpPost("listings")]
        public async Task<IActionResult> GetListings([FromBody] AnalysisRequest request)
        {
            if (request == null) return BadRequest();
            var results = await _statisticsService.GetListingsAsync(request, HttpContext.RequestAborted);
            return Ok(results);
        }

        [HttpGet("health")]
        public IActionResult GetHealth() => Ok(new { status = "Healthy" });
    }
}
