using System.Net;
using System.Text;
using ApiAggregator.Api.Configuration;
using ApiAggregator.Api.Infrastructure;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly;

namespace ApiAggregator.Tests.Services
{
    public class NewsServiceTests
    {
        // ─── Shared helpers ───────────────────────────────────────────────────────

        private static IOptions<ApiSettings> BuildSettings() =>
            Options.Create(new ApiSettings
            {
                Apis = new Dictionary<string, ServiceApiSettings>
                {
                    ["News"] = new ServiceApiSettings
                    {
                        BaseUrl = "https://hacker-news.firebaseio.com/v0/",
                        Endpoint = "topstories.json",
                        CacheDurationSeconds = 120,
                        TimeoutSeconds = 5
                    }
                },
                Resilience = new ResilienceSettings()
            });

        private static IResiliencePolicies BuildPassthroughPolicies()
        {
            var passthrough = new ResiliencePipelineBuilder().Build();
            var mock = new Mock<IResiliencePolicies>();
            mock.Setup(p => p.GetPipeline(It.IsAny<string>())).Returns(passthrough);
            return mock.Object;
        }

        private static Mock<ICacheService> BuildCacheServiceThatCallsFactory<T>()
        {
            var mock = new Mock<ICacheService>();
            mock.Setup(c => c.GetOrCreateAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<T>>>(),
                    It.IsAny<TimeSpan?>()))
                .Returns<string, Func<Task<T>>, TimeSpan?>(async (_, factory, _) => await factory());
            return mock;
        }

        // Returns the top-stories IDs list, plus individual item JSON for each
        private static HttpClient BuildHttpClientForNews(IEnumerable<long> ids)
        {
            var idJson = $"[{string.Join(",", ids)}]";

            var responses = new Dictionary<string, string>
            {
                ["topstories.json"] = idJson
            };

            long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var id in ids)
            {
                responses[$"item/{id}.json"] = $$"""
                    {
                        "id": {{id}},
                        "title": "Story {{id}}",
                        "by": "author_{{id}}",
                        "url": "https://example.com/{{id}}",
                        "score": {{id * 10}},
                        "time": {{unixTime}}
                    }
                    """;
            }

            var handler = new SequentialUrlHttpMessageHandler(responses);
            return new HttpClient(handler)
            {
                BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
            };
        }

        // ─── Happy path ───────────────────────────────────────────────────────────

        [Fact]
        public async Task FetchDataAsync_ReturnsNewsList_WhenApiSucceeds()
        {
            var ids = new long[] { 101, 102, 103 };
            var cacheService = BuildCacheServiceThatCallsFactory<List<NewsResult>>();

            var service = new NewsService(
                BuildHttpClientForNews(ids),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<NewsService>.Instance);

            var result = await service.FetchDataAsync();

            var newsList = Assert.IsType<List<NewsResult>>(result);
            Assert.Equal(3, newsList.Count);
        }

        [Fact]
        public async Task FetchDataAsync_LimitsResultsToFive_EvenIfMoreIdsReturned()
        {
            var ids = new long[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var cacheService = BuildCacheServiceThatCallsFactory<List<NewsResult>>();

            var service = new NewsService(
                BuildHttpClientForNews(ids),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<NewsService>.Instance);

            var result = await service.FetchDataAsync();

            var newsList = Assert.IsType<List<NewsResult>>(result);
            Assert.True(newsList.Count <= 5);
        }

        [Fact]
        public async Task FetchDataAsync_MapsStoryFieldsCorrectly()
        {
            var ids = new long[] { 42 };
            var cacheService = BuildCacheServiceThatCallsFactory<List<NewsResult>>();

            var service = new NewsService(
                BuildHttpClientForNews(ids),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<NewsService>.Instance);

            var result = await service.FetchDataAsync();

            var newsList = Assert.IsType<List<NewsResult>>(result);
            var story = newsList.First();
            Assert.Equal("Story 42", story.Title);
            Assert.Equal("author_42", story.Author);
            Assert.Equal("https://example.com/42", story.Url);
            Assert.Equal(420, story.Score);
        }

        [Fact]
        public async Task FetchDataAsync_ReturnsEmptyList_WhenApiReturnsEmptyIdList()
        {
            var cacheService = BuildCacheServiceThatCallsFactory<List<NewsResult>>();
            var responses = new Dictionary<string, string> { ["topstories.json"] = "[]" };
            var handler = new SequentialUrlHttpMessageHandler(responses);
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
            };

            var service = new NewsService(
                httpClient,
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<NewsService>.Instance);

            var result = await service.FetchDataAsync();

            var newsList = Assert.IsType<List<NewsResult>>(result);
            Assert.Empty(newsList);
        }

        // ─── Fallback path ────────────────────────────────────────────────────────

        [Fact]
        public async Task FetchDataAsync_ReturnsFallbackData_WhenCacheReturnsNull()
        {
            var cacheService = new Mock<ICacheService>();
            cacheService
                .Setup(c => c.GetOrCreateAsync<List<NewsResult>>(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<NewsResult>>>>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((List<NewsResult>?)null);

            var service = new NewsService(
                new HttpClient(),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<NewsService>.Instance);

            var result = await service.FetchDataAsync();

            var newsList = Assert.IsType<List<NewsResult>>(result);
            Assert.NotEmpty(newsList);
            // Fallback contains .NET / Polly related mock items
            Assert.Contains(newsList, n => n.Title.Contains(".NET") || n.Title.Contains("Polly"));
        }

        // ─── Cache key ────────────────────────────────────────────────────────────

        [Fact]
        public async Task FetchDataAsync_UsesCacheKey_WithServiceName()
        {
            var cacheService = BuildCacheServiceThatCallsFactory<List<NewsResult>>();

            var service = new NewsService(
                BuildHttpClientForNews(new long[] { }),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<NewsService>.Instance);

            await service.FetchDataAsync();

            cacheService.Verify(
                c => c.GetOrCreateAsync(
                    "cache_news",
                    It.IsAny<Func<Task<List<NewsResult>>>>(),
                    It.IsAny<TimeSpan?>()),
                Times.Once);
        }

        // ─── ServiceName ──────────────────────────────────────────────────────────

        [Fact]
        public void ServiceName_IsNews()
        {
            var service = new NewsService(
                new HttpClient(),
                new Mock<ICacheService>().Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<NewsService>.Instance);

            Assert.Equal("News", service.ServiceName);
        }
    }
}
