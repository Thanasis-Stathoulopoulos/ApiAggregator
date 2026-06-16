using System.Collections.Concurrent;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;

namespace ApiAggregator.Api.Services
{
    public class StatisticsService : IStatisticsService
    {
        private readonly ConcurrentDictionary<string, ServiceStatsTracker> _stats = new();

        public void RecordRequest(string serviceName, bool isSuccess, long responseTimeMs, string? errorMessage = null)
        {
            var tracker = _stats.GetOrAdd(serviceName.ToLowerInvariant(), _ => new ServiceStatsTracker());
            tracker.Record(isSuccess, responseTimeMs);
        }

        public IEnumerable<ApiStatistics> GetStatistics()
        {
            return _stats.Select(kvp => new ApiStatistics
            {
                ServiceName = kvp.Key,
                TotalRequests = kvp.Value.TotalRequests,
                SuccessfulRequests = kvp.Value.SuccessfulRequests,
                FailedRequests = kvp.Value.FailedRequests,
                AverageResponseTimeMs = Math.Round(kvp.Value.AverageResponseTimeMs, 2),
                Buckets = new PerformanceBuckets
                {
                    FastCount = kvp.Value.FastCount,
                    AverageCount = kvp.Value.AverageCount,
                    SlowCount = kvp.Value.SlowCount
                }
            }).OrderBy(x => x.ServiceName);
        }

        public void Reset()
        {
            _stats.Clear();
        }

        public double GetRecentAverageResponseTime(string serviceName, TimeSpan window)
        {
            if (_stats.TryGetValue(serviceName.ToLowerInvariant(), out var tracker))
            {
                return tracker.GetRecentAverage(window);
            }
            return 0;
        }

        public double GetOverallAverageResponseTime(string serviceName)
        {
            if (_stats.TryGetValue(serviceName.ToLowerInvariant(), out var tracker))
            {
                return tracker.AverageResponseTimeMs;
            }
            return 0;
        }

        private class ServiceStatsTracker
        {
            private int _totalRequests;
            private int _successfulRequests;
            private int _failedRequests;
            private long _totalResponseTimeMs;
            private int _fastCount;
            private int _averageCount;
            private int _slowCount;

            private readonly ConcurrentQueue<(DateTime Timestamp, long ResponseTimeMs)> _history = new();

            public int TotalRequests => _totalRequests;
            public int SuccessfulRequests => _successfulRequests;
            public int FailedRequests => _failedRequests;
            public double AverageResponseTimeMs => _totalRequests == 0 ? 0 : (double)_totalResponseTimeMs / _totalRequests;
            public int FastCount => _fastCount;
            public int AverageCount => _averageCount;
            public int SlowCount => _slowCount;

            public void Record(bool isSuccess, long responseTimeMs)
            {
                Interlocked.Increment(ref _totalRequests);
                if (isSuccess)
                {
                    Interlocked.Increment(ref _successfulRequests);
                }
                else
                {
                    Interlocked.Increment(ref _failedRequests);
                }

                Interlocked.Add(ref _totalResponseTimeMs, responseTimeMs);

                if (responseTimeMs < 100)
                {
                    Interlocked.Increment(ref _fastCount);
                }
                else if (responseTimeMs <= 300)
                {
                    Interlocked.Increment(ref _averageCount);
                }
                else
                {
                    Interlocked.Increment(ref _slowCount);
                }

                _history.Enqueue((DateTime.UtcNow, responseTimeMs));

                // Clean up history older than 10 minutes to prevent memory leaks
                var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10));
                while (_history.TryPeek(out var item) && item.Timestamp < cutoff)
                {
                    _history.TryDequeue(out _);
                }
            }

            public double GetRecentAverage(TimeSpan window)
            {
                var cutoff = DateTime.UtcNow.Subtract(window);
                var recentItems = _history.Where(x => x.Timestamp >= cutoff).ToList();
                if (recentItems.Count == 0)
                {
                    return 0;
                }
                return recentItems.Average(x => x.ResponseTimeMs);
            }
        }
    }
}
