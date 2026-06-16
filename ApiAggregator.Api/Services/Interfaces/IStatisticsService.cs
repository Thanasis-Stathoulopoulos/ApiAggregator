using ApiAggregator.Api.Models;

namespace ApiAggregator.Api.Services.Interfaces
{
    public interface IStatisticsService
    {
        void RecordRequest(string serviceName, bool isSuccess, long responseTimeMs, string? errorMessage = null);
        IEnumerable<ApiStatistics> GetStatistics();
        void Reset();
        double GetRecentAverageResponseTime(string serviceName, TimeSpan window);
        double GetOverallAverageResponseTime(string serviceName);
    }
}
