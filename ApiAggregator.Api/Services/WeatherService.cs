using System.Text.Json;
using ApiAggregator.Api.Configuration;
using ApiAggregator.Api.Infrastructure;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.Services
{
    public class WeatherService : IExternalApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;
        private readonly IResiliencePolicies _resiliencePolicies;
        private readonly ApiSettings _settings;
        private readonly ILogger<WeatherService> _logger;

        public string ServiceName => "Weather";

        public WeatherService(
            HttpClient httpClient,
            ICacheService cacheService,
            IResiliencePolicies resiliencePolicies,
            IOptions<ApiSettings> settings,
            ILogger<WeatherService> logger)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            _resiliencePolicies = resiliencePolicies;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<object> FetchDataAsync(CancellationToken cancellationToken = default)
        {
            var config = _settings.Apis[ServiceName] ?? throw new InvalidOperationException("Weather API config missing");
            var cacheKey = $"cache_{ServiceName.ToLowerInvariant()}";
            var cacheDuration = TimeSpan.FromSeconds(config.CacheDurationSeconds);

            var data = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var pipeline = _resiliencePolicies.GetPipeline(ServiceName);
                return await pipeline.ExecuteAsync(async token =>
                {
                    _logger.LogInformation("Calling Weather API: {BaseUrl}{Endpoint}", config.BaseUrl, config.Endpoint);
                    var response = await _httpClient.GetAsync(config.Endpoint, token);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync(token);
                    using var doc = JsonDocument.Parse(json);
                    var current = doc.RootElement.GetProperty("current");

                    return new WeatherResult
                    {
                        Latitude = doc.RootElement.GetProperty("latitude").GetDouble(),
                        Longitude = doc.RootElement.GetProperty("longitude").GetDouble(),
                        Time = current.GetProperty("time").GetString() ?? string.Empty,
                        Temperature = current.GetProperty("temperature_2m").GetDouble(),
                        WindSpeed = current.GetProperty("wind_speed_10m").GetDouble(),
                        TemperatureUnit = "°C",
                        WindSpeedUnit = "km/h"
                    };
                }, cancellationToken);
            }, cacheDuration);

            return data ?? GetFallbackData();
        }

        private WeatherResult GetFallbackData()
        {
            _logger.LogWarning("Returning Weather Fallback Mock Data");
            return new WeatherResult
            {
                Latitude = 52.52,
                Longitude = 13.41,
                Time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm"),
                Temperature = 21.5,
                WindSpeed = 12.0,
                TemperatureUnit = "°C",
                WindSpeedUnit = "km/h"
            };
        }
    }
}
