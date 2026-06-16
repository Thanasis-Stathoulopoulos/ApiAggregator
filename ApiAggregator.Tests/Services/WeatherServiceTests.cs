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
    public class WeatherServiceTests
    {
        // ─── Shared helpers ───────────────────────────────────────────────────────

        private static IOptions<ApiSettings> BuildSettings() =>
            Options.Create(new ApiSettings
            {
                Apis = new Dictionary<string, ServiceApiSettings>
                {
                    ["Weather"] = new ServiceApiSettings
                    {
                        BaseUrl = "https://api.open-meteo.com/v1/",
                        Endpoint = "forecast?latitude=52.52&longitude=13.41&current=temperature_2m,wind_speed_10m",
                        CacheDurationSeconds = 60,
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
            return new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com/v1/") };
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

        private const string ValidWeatherJson = """
            {
                "latitude": 52.52,
                "longitude": 13.41,
                "current": {
                    "time": "2024-01-15T10:00",
                    "temperature_2m": 18.5,
                    "wind_speed_10m": 14.2
                }
            }
            """;

        // ─── Happy path ───────────────────────────────────────────────────────────

        [Fact]
        public async Task FetchDataAsync_ReturnsWeatherResult_WhenApiSucceeds()
        {
            var cacheService = BuildCacheServiceThatCallsFactory<WeatherResult>();
            var service = new WeatherService(
                BuildHttpClient(ValidWeatherJson),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<WeatherService>.Instance);

            var result = await service.FetchDataAsync();

            var weather = Assert.IsType<WeatherResult>(result);
            Assert.Equal(52.52, weather.Latitude);
            Assert.Equal(13.41, weather.Longitude);
            Assert.Equal(18.5, weather.Temperature);
            Assert.Equal(14.2, weather.WindSpeed);
            Assert.Equal("2024-01-15T10:00", weather.Time);
            Assert.Equal("°C", weather.TemperatureUnit);
            Assert.Equal("km/h", weather.WindSpeedUnit);
        }

        [Fact]
        public async Task FetchDataAsync_UsesCacheKey_WithServiceName()
        {
            var cacheService = BuildCacheServiceThatCallsFactory<WeatherResult>();
            var service = new WeatherService(
                BuildHttpClient(ValidWeatherJson),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<WeatherService>.Instance);

            await service.FetchDataAsync();

            cacheService.Verify(
                c => c.GetOrCreateAsync(
                    "cache_weather",
                    It.IsAny<Func<Task<WeatherResult>>>(),
                    It.IsAny<TimeSpan?>()),
                Times.Once);
        }

        // ─── Fallback path ────────────────────────────────────────────────────────

        [Fact]
        public async Task FetchDataAsync_ReturnsFallbackData_WhenCacheReturnsNull()
        {
            // Arrange: cache returns null (e.g. factory failed gracefully)
            var cacheService = new Mock<ICacheService>();
            cacheService
                .Setup(c => c.GetOrCreateAsync<WeatherResult>(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<WeatherResult>>>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync((WeatherResult?)null);

            var service = new WeatherService(
                BuildHttpClient(ValidWeatherJson),
                cacheService.Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<WeatherService>.Instance);

            var result = await service.FetchDataAsync();

            var weather = Assert.IsType<WeatherResult>(result);
            // Fallback has hardcoded temperature of 21.5
            Assert.Equal(21.5, weather.Temperature);
            Assert.Equal(52.52, weather.Latitude);
        }

        // ─── ServiceName ──────────────────────────────────────────────────────────

        [Fact]
        public void ServiceName_IsWeather()
        {
            var service = new WeatherService(
                BuildHttpClient("{}"),
                new Mock<ICacheService>().Object,
                BuildPassthroughPolicies(),
                BuildSettings(),
                NullLogger<WeatherService>.Instance);

            Assert.Equal("Weather", service.ServiceName);
        }
    }
}
