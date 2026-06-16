using ApiAggregator.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        [HttpGet]
        public IActionResult GetStatistics()
        {
            var stats = _statisticsService.GetStatistics();
            return Ok(stats);
        }

        [HttpPost("reset")]
        public IActionResult ResetStatistics()
        {
            _statisticsService.Reset();
            return Ok(new { Message = "Statistics have been reset successfully." });
        }
    }
}
