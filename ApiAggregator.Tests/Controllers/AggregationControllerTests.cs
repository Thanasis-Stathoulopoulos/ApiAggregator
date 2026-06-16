using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApiAggregator.Api.Controllers;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ApiAggregator.Tests.Controllers
{
    public class AggregationControllerTests
    {
        private readonly Mock<IExternalApiService> _weatherServiceMock;
        private readonly Mock<IExternalApiService> _newsServiceMock;
        private readonly Mock<IExternalApiService> _githubServiceMock;
        private readonly Mock<IStatisticsService> _statsServiceMock;
        private readonly Mock<IMemoryCache> _memoryCacheMock;
        private readonly List<IExternalApiService> _services;

        public AggregationControllerTests()
        {
            _weatherServiceMock = new Mock<IExternalApiService>();
            _weatherServiceMock.Setup(s => s.ServiceName).Returns("Weather");
            _weatherServiceMock.Setup(s => s.FetchDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WeatherResult
                {
                    Latitude = 1.0,
                    Longitude = 2.0,
                    Temperature = 15.5,
                    WindSpeed = 5.0,
                    Time = "2026-06-16T12:00",
                    TemperatureUnit = "°C",
                    WindSpeedUnit = "km/h"
                });

            _newsServiceMock = new Mock<IExternalApiService>();
            _newsServiceMock.Setup(s => s.ServiceName).Returns("News");
            _newsServiceMock.Setup(s => s.FetchDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NewsResult>
                {
                    new NewsResult { Title = "Learn .NET Core", Author = "John", Url = "https://example.com/net", Score = 100 },
                    new NewsResult { Title = "Hacker News story", Author = "Alice", Url = "https://example.com/hn", Score = 50 }
                });

            _githubServiceMock = new Mock<IExternalApiService>();
            _githubServiceMock.Setup(s => s.ServiceName).Returns("GitHub");
            _githubServiceMock.Setup(s => s.FetchDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GitHubResult
                {
                    Username = "dev-octocat",
                    Name = "Dev Octo",
                    Company = "GitHub",
                    Bio = "Learning and coding .net.",
                    PublicRepos = 10,
                    Followers = 100,
                    Following = 5,
                    HtmlUrl = "https://github.com/dev-octocat"
                });

            _statsServiceMock = new Mock<IStatisticsService>();

            _memoryCacheMock = new Mock<IMemoryCache>();
            object? dummy;
            _memoryCacheMock.Setup(mc => mc.TryGetValue(It.IsAny<object>(), out dummy)).Returns(false);

            _services = new List<IExternalApiService>
            {
                _weatherServiceMock.Object,
                _newsServiceMock.Object,
                _githubServiceMock.Object
            };
        }

        private AggregationController GetController()
        {
            return new AggregationController(
                _services,
                _statsServiceMock.Object,
                _memoryCacheMock.Object,
                NullLogger<AggregationController>.Instance);
        }

        [Fact]
        public async Task GetAggregatedData_QueriesAllServices_WhenNoFilterSpecified()
        {
            var controller = GetController();
            var filter = new FilterParams();

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            Assert.NotNull(result.Weather);
            Assert.NotNull(result.News);
            Assert.NotNull(result.GitHub);
            Assert.Equal(3, result.Metadata.Count);

            _weatherServiceMock.Verify(s => s.FetchDataAsync(It.IsAny<CancellationToken>()), Times.Once);
            _newsServiceMock.Verify(s => s.FetchDataAsync(It.IsAny<CancellationToken>()), Times.Once);
            _githubServiceMock.Verify(s => s.FetchDataAsync(It.IsAny<CancellationToken>()), Times.Once);
            _statsServiceMock.Verify(s => s.RecordRequest(It.IsAny<string>(), true, It.IsAny<long>(), null), Times.Exactly(3));
        }

        [Fact]
        public async Task GetAggregatedData_QueriesOnlyRequestedServices_WhenServicesFilterUsed()
        {
            var controller = GetController();
            var filter = new FilterParams { Services = "weather,github" };

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            Assert.NotNull(result.Weather);
            Assert.Null(result.News);
            Assert.NotNull(result.GitHub);
            Assert.Equal(2, result.Metadata.Count);

            _weatherServiceMock.Verify(s => s.FetchDataAsync(It.IsAny<CancellationToken>()), Times.Once);
            _newsServiceMock.Verify(s => s.FetchDataAsync(It.IsAny<CancellationToken>()), Times.Never);
            _githubServiceMock.Verify(s => s.FetchDataAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAggregatedData_AppliesKeywordFilter_ToNewsAndGitHub()
        {
            var controller = GetController();
            var filter = new FilterParams { Keyword = ".net" };

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            Assert.NotNull(result.News);
            Assert.Single(result.News);
            Assert.Equal("Learn .NET Core", result.News[0].Title);

            Assert.NotNull(result.GitHub); // "dotnet" is in Bio
        }

        [Fact]
        public async Task GetAggregatedData_NullsGitHubResult_WhenKeywordDoesNotMatch()
        {
            var controller = GetController();
            var filter = new FilterParams { Keyword = "nonexistent" };

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            Assert.Empty(result.News!);
            Assert.Null(result.GitHub);
        }

        [Fact]
        public async Task GetAggregatedData_SortsMetadataByName_Ascending()
        {
            var controller = GetController();
            var filter = new FilterParams { SortBy = "name", SortOrder = "asc" };

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            var keys = result.Metadata.Keys.ToList();
            Assert.Equal(new List<string> { "GitHub", "News", "Weather" }, keys);
        }

        [Fact]
        public async Task GetAggregatedData_SortsMetadataByName_Descending()
        {
            var controller = GetController();
            var filter = new FilterParams { SortBy = "name", SortOrder = "desc" };

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            var keys = result.Metadata.Keys.ToList();
            Assert.Equal(new List<string> { "Weather", "News", "GitHub" }, keys);
        }

        [Fact]
        public async Task GetAggregatedData_HandlesServiceFailure_Gracefully()
        {
            _weatherServiceMock.Setup(s => s.FetchDataAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("API Timeout"));

            var controller = GetController();
            var filter = new FilterParams { Services = "weather" };

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            Assert.Null(result.Weather);
            var meta = result.Metadata["Weather"];
            Assert.False(meta.IsSuccess);
            Assert.Equal("API Timeout", meta.ErrorMessage);

            _statsServiceMock.Verify(s => s.RecordRequest("Weather", false, It.IsAny<long>(), "API Timeout"), Times.Once);
        }

        [Fact]
        public async Task GetAggregatedData_ReportsIsCachedTrue_WhenCacheHasValue()
        {
            object? dummy;
            _memoryCacheMock.Setup(mc => mc.TryGetValue(It.Is<object>(k => k.ToString() == "cache_weather"), out dummy))
                .Returns(true);

            var controller = GetController();
            var filter = new FilterParams { Services = "weather" };

            var response = await controller.GetAggregatedData(filter, default);

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<AggregatedResult>(okResult.Value);

            Assert.True(result.Metadata["Weather"].IsCached);
        }
    }
}
