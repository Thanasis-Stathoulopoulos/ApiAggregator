using System.Text.Json;
using ApiAggregator.Api.Configuration;
using ApiAggregator.Api.Infrastructure;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.Services
{
    public class NewsService : IExternalApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly IResiliencePolicies _resiliencePolicies;
        private readonly ApiSettings _settings;
        private readonly ILogger<NewsService> _logger;

        public string ServiceName => "News";

        public NewsService(
            HttpClient httpClient,
            ICacheService cacheService,
            IResiliencePolicies resiliencePolicies,
            IOptions<ApiSettings> settings,
            ILogger<NewsService> logger)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _resiliencePolicies = resiliencePolicies;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<object> FetchDataAsync(CancellationToken cancellationToken = default)
        {
            var config = _settings.Apis[ServiceName] ?? throw new InvalidOperationException("News API config missing");
            var cacheKey = $"cache_{ServiceName.ToLowerInvariant()}";
            var cacheDuration = TimeSpan.FromSeconds(config.CacheDurationSeconds);

            var data = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var pipeline = _resiliencePolicies.GetPipeline(ServiceName);
                return await pipeline.ExecuteAsync(async token =>
                {
                    _logger.LogInformation("Calling News API: {BaseUrl}{Endpoint}", config.BaseUrl, config.Endpoint);
                    var response = await _httpClient.GetAsync(config.Endpoint, token);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(token);
                    var ids = JsonSerializer.Deserialize<List<long>>(json);
                    if (ids == null || ids.Count == 0)
                    {
                        return new List<NewsResult>();
                    }

                    // Fetch details of top 5 stories in parallel
                    var targetIds = ids.Take(5).ToList();
                    var fetchTasks = targetIds.Select(id => FetchStoryDetailsAsync(id, token));
                    var results = await Task.WhenAll(fetchTasks);

                    return results.Where(r => r != null).Cast<NewsResult>().ToList();
                }, cancellationToken);
            }, cacheDuration);

            return data ?? GetFallbackData();
        }

        private async Task<NewsResult?> FetchStoryDetailsAsync(long id, CancellationToken token)
        {
            try
            {
                var itemUrl = $"item/{id}.json";
                var response = await _httpClient.GetAsync(itemUrl, token);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync(token);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var title = root.GetProperty("title").GetString() ?? "No Title";
                var author = root.GetProperty("by").GetString() ?? "Unknown";
                var url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                var score = root.TryGetProperty("score", out var scoreProp) ? scoreProp.GetInt32() : 0;
                var timeUnix = root.GetProperty("time").GetInt64();
                var publishedAt = DateTimeOffset.FromUnixTimeSeconds(timeUnix).UtcDateTime;

                return new NewsResult
                {
                    Title = title,
                    Author = author,
                    Url = url,
                    Score = score,
                    PublishedAt = publishedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to fetch details for news item {Id}: {Error}", id, ex.Message);
                return null;
            }
        }

        private List<NewsResult> GetFallbackData()
        {
            _logger.LogWarning("Returning News Fallback Mock Data");
            return new List<NewsResult>
            {
                new NewsResult
                {
                    Title = "Microsoft Announces .NET 8.0 with Improved Performance",
                    Author = "john_doe",
                    Url = "https://devblogs.microsoft.com/dotnet/announcing-dotnet-8/",
                    Score = 245,
                    PublishedAt = DateTime.UtcNow.AddHours(-2)
                },
                new NewsResult
                {
                    Title = "Polly v8.0 Resilience Policies Released",
                    Author = "jane_smith",
                    Url = "https://github.com/App-vNext/Polly",
                    Score = 180,
                    PublishedAt = DateTime.UtcNow.AddHours(-5)
                },
                new NewsResult
                {
                    Title = "Hacker News Clone built in ASP.NET Core Minimal APIs",
                    Author = "csharp_dev",
                    Url = "https://github.com/csharp/hn-clone",
                    Score = 95,
                    PublishedAt = DateTime.UtcNow.AddHours(-10)
                }
            };
        }
    }
}
