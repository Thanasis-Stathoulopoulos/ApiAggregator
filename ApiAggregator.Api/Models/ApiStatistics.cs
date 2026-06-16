namespace ApiAggregator.Api.Models
{
    public class ApiStatistics
    {
        public string ServiceName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public PerformanceBuckets Buckets { get; set; } = new();
    }

    public class PerformanceBuckets
    {
        public int FastCount { get; set; }      // < 100ms
        public int AverageCount { get; set; }   // 100ms - 300ms
        public int SlowCount { get; set; }      // > 300ms
    }
}
