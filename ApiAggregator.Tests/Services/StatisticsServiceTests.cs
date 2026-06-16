using ApiAggregator.Api.Services;

namespace ApiAggregator.Tests.Services
{
    public class StatisticsServiceTests
    {
        private readonly StatisticsService _sut = new();

        // ─── RecordRequest ────────────────────────────────────────────────────────

        [Fact]
        public void RecordRequest_IncrementsTotal()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 50);

            var stat = _sut.GetStatistics().Single(s => s.ServiceName == "weather");
            Assert.Equal(1, stat.TotalRequests);
        }

        [Fact]
        public void RecordRequest_Successful_IncrementsSuccessCount()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 50);

            var stat = _sut.GetStatistics().Single(s => s.ServiceName == "weather");
            Assert.Equal(1, stat.SuccessfulRequests);
            Assert.Equal(0, stat.FailedRequests);
        }

        [Fact]
        public void RecordRequest_Failed_IncrementsFailureCount()
        {
            _sut.RecordRequest("news", isSuccess: false, responseTimeMs: 500);

            var stat = _sut.GetStatistics().Single(s => s.ServiceName == "news");
            Assert.Equal(0, stat.SuccessfulRequests);
            Assert.Equal(1, stat.FailedRequests);
        }

        [Fact]
        public void RecordRequest_ServiceNameNormalizedToLowercase()
        {
            _sut.RecordRequest("GitHub", isSuccess: true, responseTimeMs: 80);
            _sut.RecordRequest("github", isSuccess: true, responseTimeMs: 80);

            // Both should land in the same bucket
            var stats = _sut.GetStatistics().ToList();
            Assert.Single(stats);
            Assert.Equal(2, stats[0].TotalRequests);
        }

        // ─── Performance Buckets ──────────────────────────────────────────────────

        [Fact]
        public void RecordRequest_FastBucket_WhenUnder100Ms()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 99);

            var stat = _sut.GetStatistics().Single();
            Assert.Equal(1, stat.Buckets.FastCount);
            Assert.Equal(0, stat.Buckets.AverageCount);
            Assert.Equal(0, stat.Buckets.SlowCount);
        }

        [Fact]
        public void RecordRequest_AverageBucket_WhenBetween100And300Ms()
        {
            _sut.RecordRequest("news", isSuccess: true, responseTimeMs: 100);
            _sut.RecordRequest("news", isSuccess: true, responseTimeMs: 300);

            var stat = _sut.GetStatistics().Single();
            Assert.Equal(0, stat.Buckets.FastCount);
            Assert.Equal(2, stat.Buckets.AverageCount);
            Assert.Equal(0, stat.Buckets.SlowCount);
        }

        [Fact]
        public void RecordRequest_SlowBucket_WhenOver300Ms()
        {
            _sut.RecordRequest("github", isSuccess: true, responseTimeMs: 301);

            var stat = _sut.GetStatistics().Single();
            Assert.Equal(0, stat.Buckets.FastCount);
            Assert.Equal(0, stat.Buckets.AverageCount);
            Assert.Equal(1, stat.Buckets.SlowCount);
        }

        [Fact]
        public void RecordRequest_BucketBoundaries_ExactlyAt100MsIsAverage()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 100);

            var stat = _sut.GetStatistics().Single();
            Assert.Equal(0, stat.Buckets.FastCount);
            Assert.Equal(1, stat.Buckets.AverageCount);
        }

        [Fact]
        public void RecordRequest_BucketBoundaries_ExactlyAt300MsIsAverage()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 300);

            var stat = _sut.GetStatistics().Single();
            Assert.Equal(1, stat.Buckets.AverageCount);
            Assert.Equal(0, stat.Buckets.SlowCount);
        }

        // ─── Average Response Time ────────────────────────────────────────────────

        [Fact]
        public void GetStatistics_ReturnsCorrectAverageResponseTime()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 100);
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 200);
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 300);

            var stat = _sut.GetStatistics().Single();
            Assert.Equal(200.0, stat.AverageResponseTimeMs);
        }

        // ─── Reset ────────────────────────────────────────────────────────────────

        [Fact]
        public void Reset_ClearsAllStatistics()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 50);
            _sut.RecordRequest("news", isSuccess: false, responseTimeMs: 500);

            _sut.Reset();

            Assert.Empty(_sut.GetStatistics());
        }

        // ─── Windowed Average ─────────────────────────────────────────────────────

        [Fact]
        public void GetRecentAverageResponseTime_ReturnsZero_WhenNoRequests()
        {
            var result = _sut.GetRecentAverageResponseTime("weather", TimeSpan.FromMinutes(5));
            Assert.Equal(0, result);
        }

        [Fact]
        public void GetRecentAverageResponseTime_ReturnsAverage_ForRecentRequests()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 100);
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 200);

            // All requests are recent (just recorded)
            var result = _sut.GetRecentAverageResponseTime("weather", TimeSpan.FromMinutes(5));
            Assert.Equal(150.0, result);
        }

        [Fact]
        public void GetOverallAverageResponseTime_ReturnsZero_WhenServiceUnknown()
        {
            var result = _sut.GetOverallAverageResponseTime("nonexistent");
            Assert.Equal(0, result);
        }

        // ─── Thread Safety ────────────────────────────────────────────────────────

        [Fact]
        public async Task RecordRequest_IsThreadSafe_UnderConcurrentLoad()
        {
            const int iterations = 1000;

            var tasks = Enumerable.Range(0, iterations)
                .Select(_ => Task.Run(() => _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 50)));

            await Task.WhenAll(tasks);

            var stat = _sut.GetStatistics().Single(s => s.ServiceName == "weather");
            Assert.Equal(iterations, stat.TotalRequests);
            Assert.Equal(iterations, stat.SuccessfulRequests);
        }

        // ─── Multiple Services ────────────────────────────────────────────────────

        [Fact]
        public void GetStatistics_ReturnsAllServices_OrderedByName()
        {
            _sut.RecordRequest("weather", isSuccess: true, responseTimeMs: 50);
            _sut.RecordRequest("github", isSuccess: true, responseTimeMs: 100);
            _sut.RecordRequest("news", isSuccess: true, responseTimeMs: 150);

            var stats = _sut.GetStatistics().ToList();
            Assert.Equal(3, stats.Count);
            Assert.Equal("github", stats[0].ServiceName);
            Assert.Equal("news", stats[1].ServiceName);
            Assert.Equal("weather", stats[2].ServiceName);
        }
    }
}
