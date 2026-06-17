using ApiAggregator.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Api.Controllers
{
    /// <summary>
    /// Controller handling in-memory performance and request statistics.
    /// </summary>
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

        /// <summary>
        /// Retrieves the recorded statistics for all external services.
        /// </summary>
        /// <returns>A collection of statistics per service.</returns>
        /// <response code="200">Returns the service statistics.</response>
        /// <response code="401">If the user is unauthorized.</response>
        [HttpGet]
        [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<Models.ApiStatistics>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetStatistics()
        {
            var stats = _statisticsService.GetStatistics();
            return Ok(stats);
        }

        /// <summary>
        /// Resets all recorded performance and request statistics.
        /// </summary>
        /// <returns>A confirmation message.</returns>
        /// <response code="200">If the statistics were successfully reset.</response>
        /// <response code="401">If the user is unauthorized.</response>
        [HttpPost("reset")]
        [ProducesResponseType(typeof(ResetStatisticsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult ResetStatistics()
        {
            _statisticsService.Reset();
            return Ok(new ResetStatisticsResponse { Message = "Statistics have been reset successfully." });
        }
    }

    /// <summary>
    /// Represents the response returned after resetting statistics.
    /// </summary>
    public class ResetStatisticsResponse
    {
        /// <summary>
        /// The success message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
