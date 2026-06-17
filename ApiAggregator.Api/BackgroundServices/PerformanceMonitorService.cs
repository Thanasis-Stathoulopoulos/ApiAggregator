using System.Linq;
using ApiAggregator.Api.Services.Interfaces;

namespace ApiAggregator.Api.BackgroundServices
{
    public class PerformanceMonitorService : BackgroundService
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ILogger<PerformanceMonitorService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromSeconds(10);
        private readonly TimeSpan _window = TimeSpan.FromMinutes(5);

        public PerformanceMonitorService(IStatisticsService statisticsService, ILogger<PerformanceMonitorService> logger)
        {
            _statisticsService = statisticsService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Performance Monitor Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    AnalyzePerformance();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while analyzing API performance.");
                }

                await Task.Delay(_period, stoppingToken);
            }

            _logger.LogInformation("Performance Monitor Service is stopping.");
        }

        private void AnalyzePerformance()
        {
            var services = _statisticsService.GetStatistics()
                .Select(s => s.ServiceName)
                .ToList();

            foreach (var service in services)
            {
                var overallAvg = _statisticsService.GetOverallAverageResponseTime(service);
                var recentAvg = _statisticsService.GetRecentAverageResponseTime(service, _window);

                if (recentAvg > 0 && overallAvg > 0)
                {
                    if (recentAvg > 1.5 * overallAvg)
                    {
                        _logger.LogWarning(
                            "PERFORMANCE ANOMALY DETECTED: Service '{Service}' average latency in the last 5 minutes is {RecentAvg:F2}ms, " +
                            "which is over 50% higher than its historical average of {OverallAvg:F2}ms.",
                            service, recentAvg, overallAvg);
                    }
                }
            }
        }
    }
}
