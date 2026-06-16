using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ApiAggregator.Api.Configuration;
using ApiAggregator.Api.Infrastructure;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Xunit;

namespace ApiAggregator.Tests.Services
{
    public class GitHubServiceTests
    {
        private static IOptions<ApiSettings> BuildSettings() =>
            Options.Create(new ApiSettings
            {
                Apis = new Dictionary<string, ServiceApiSettings>
                {
                    ["GitHub"] = new ServiceApiSettings
                    {
                        BaseUrl = "https://api.github.com/",
                        Endpoint = "users/octocat",
                        CacheDurationSeconds = 180,
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

        private static HttpClient BuildHttpClient(string jsonResponse, HttpStatusCode status = HttpStatusCode.OK)
        {
            var handler = new TestHttpMessageHandler(new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
            return new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
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

        private const string ValidGitHubJson = """
            {
                "login": "octocat",
                "name": "The Octocat",
                "company": "GitHub",
                "bio": "Testing user profile bio.",
                "public_repos": 8,
                "followers": 20,
                "following": 9,
                "html_url": "https://github.com/octocat"
            }
            """;

        [Fact]
        public async Task FetchDataAsync_ReturnsGitHubResult_WhenApiSucceeds()
        {
            var cacheService = BuildCacheServiceThatCallsFactory<GitHubResult>();
            var service = new GitHubService(
                BuildHttpClient(ValidGitHubJson),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<GitHubService>.Instance);

            var result = await service.FetchDataAsync();

            var github = Assert.IsType<GitHubResult>(result);
            Assert.Equal("octocat", github.Username);
            Assert.Equal("The Octocat", github.Name);
            Assert.Equal("GitHub", github.Company);
            Assert.Equal("Testing user profile bio.", github.Bio);
            Assert.Equal(8, github.PublicRepos);
            Assert.Equal(20, github.Followers);
            Assert.Equal(9, github.Following);
            Assert.Equal("https://github.com/octocat", github.HtmlUrl);
        }

        [Fact]
        public async Task FetchDataAsync_UsesCacheKey_WithServiceName()
        {
            var cacheService = BuildCacheServiceThatCallsFactory<GitHubResult>();
            var service = new GitHubService(
                BuildHttpClient(ValidGitHubJson),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<GitHubService>.Instance);

            await service.FetchDataAsync();

            cacheService.Verify(
                c => c.GetOrCreateAsync(
                    "cache_github",
                    It.IsAny<Func<Task<GitHubResult>>>(),
                    It.IsAny<TimeSpan?>()),
                Times.Once);
        }

        [Fact]
        public async Task FetchDataAsync_ReturnsFallbackData_WhenCacheReturnsNull()
        {
            var cacheService = new Mock<ICacheService>();
            cacheService
                .Setup(c => c.GetOrCreateAsync<GitHubResult>(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<GitHubResult>>>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((GitHubResult?)null);

            var service = new GitHubService(
                BuildHttpClient(ValidGitHubJson),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<GitHubService>.Instance);

            var result = await service.FetchDataAsync();

            var github = Assert.IsType<GitHubResult>(result);
            Assert.Equal("fallback-octocat", github.Username);
            Assert.Equal("Fallback Octocat", github.Name);
            Assert.Equal(42, github.PublicRepos);
        }

        [Fact]
        public void ServiceName_IsGitHub()
        {
            var service = new GitHubService(
                BuildHttpClient("{}"),
                new Mock<ICacheService>().Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<GitHubService>.Instance);

            Assert.Equal("GitHub", service.ServiceName);
        }
    }
}
