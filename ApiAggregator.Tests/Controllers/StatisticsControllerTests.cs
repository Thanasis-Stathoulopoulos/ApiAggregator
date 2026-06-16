using System.Collections.Generic;
using ApiAggregator.Api.Controllers;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ApiAggregator.Tests.Controllers
{
    public class StatisticsControllerTests
    {
        private readonly Mock<IStatisticsService> _statsServiceMock;

        public StatisticsControllerTests()
        {
            _statsServiceMock = new Mock<IStatisticsService>();
        }

        private StatisticsController GetController()
        {
            return new StatisticsController(_statsServiceMock.Object);
        }

        [Fact]
        public void GetStatistics_ReturnsOkWithStats()
        {
            var mockStats = new List<ApiStatistics>
            {
                new ApiStatistics
                {
                    ServiceName = "Weather",
                    TotalRequests = 10,
                    SuccessfulRequests = 9,
                    FailedRequests = 1,
                    AverageResponseTimeMs = 150
                }
            };
            _statsServiceMock.Setup(s => s.GetStatistics()).Returns(mockStats);
            var controller = GetController();

            var response = controller.GetStatistics();

            var okResult = Assert.IsType<OkObjectResult>(response);
            var result = Assert.IsType<List<ApiStatistics>>(okResult.Value);
            Assert.Single(result);
            Assert.Equal("Weather", result[0].ServiceName);
            Assert.Equal(10, result[0].TotalRequests);
            _statsServiceMock.Verify(s => s.GetStatistics(), Times.Once);
        }

        [Fact]
        public void ResetStatistics_CallsResetAndReturnsOk()
        {
            var controller = GetController();

            var response = controller.ResetStatistics();

            var okResult = Assert.IsType<OkObjectResult>(response);
            _statsServiceMock.Verify(s => s.Reset(), Times.Once);
        }
    }
}
