using System.Net.Http.Headers;
using System.Text.Json;
using ApiAggregator.Api.Configuration;
using ApiAggregator.Api.Infrastructure;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.Services
{
    public class GitHubService : IExternalApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly IResiliencePolicies _resiliencePolicies;
        private readonly ApiSettings _settings;
        private readonly ILogger<GitHubService> _logger;

        public string ServiceName => "GitHub";

        public GitHubService(
            HttpClient httpClient,
            ICacheService cacheService,
            IResiliencePolicies resiliencePolicies,
            IOptions<ApiSettings> settings,
            ILogger<GitHubService> logger)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _resiliencePolicies = resiliencePolicies;
            _settings = settings.Value;
            _logger = logger;
            
            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ApiAggregator", "1.0"));
            }
        }

        public async Task<object> FetchDataAsync(CancellationToken cancellationToken = default)
        {
            var config = _settings.Apis[ServiceName] ?? throw new InvalidOperationException("GitHub API config missing");
            var cacheKey = $"cache_{ServiceName.ToLowerInvariant()}";
            var cacheDuration = TimeSpan.FromSeconds(config.CacheDurationSeconds);

            var data = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var pipeline = _resiliencePolicies.GetPipeline(ServiceName);
                return await pipeline.ExecuteAsync(async token =>
                {
                    _logger.LogInformation("Calling GitHub API: {BaseUrl}{Endpoint}", config.BaseUrl, config.Endpoint);
                    var response = await _httpClient.GetAsync(config.Endpoint, token);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(token);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    return new GitHubResult
                    {
                        Username = root.GetProperty("login").GetString() ?? string.Empty,
                        Name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                        Company = root.TryGetProperty("company", out var compProp) ? compProp.GetString() ?? "" : "",
                        Bio = root.TryGetProperty("bio", out var bioProp) ? bioProp.GetString() ?? "" : "",
                        PublicRepos = root.GetProperty("public_repos").GetInt32(),
                        Followers = root.GetProperty("followers").GetInt32(),
                        Following = root.GetProperty("following").GetInt32(),
                        HtmlUrl = root.GetProperty("html_url").GetString() ?? string.Empty
                    };
                }, cancellationToken);
            }, cacheDuration);

            return data ?? GetFallbackData();
        }

        private GitHubResult GetFallbackData()
        {
            _logger.LogWarning("Returning GitHub Fallback Mock Data");
            return new GitHubResult
            {
                Username = "fallback-octocat",
                Name = "Fallback Octocat",
                Company = "Fallback GitHub Org",
                Bio = "Self-defined fallback bio due to API rate limit or outage.",
                PublicRepos = 42,
                Followers = 1337,
                Following = 42,
                HtmlUrl = "https://github.com/octocat"
            };
        }
    }
}
