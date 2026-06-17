using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApiAggregator.Api.BackgroundServices;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ApiAggregator.Tests.Services
{
    public class PerformanceMonitorServiceTests
    {
        [Fact]
        public async Task PerformanceMonitorService_LogsWarning_WhenRecentLatencyExceedsBaselineByFiftyPercent()
        {
            // Arrange
            var statsMock = new Mock<IStatisticsService>();
            var loggerMock = new Mock<ILogger<PerformanceMonitorService>>();

            statsMock.Setup(s => s.GetStatistics()).Returns(new List<ApiStatistics>
            {
                new ApiStatistics { ServiceName = "weather" }
            });

            statsMock.Setup(s => s.GetOverallAverageResponseTime("weather")).Returns(100.0);
            statsMock.Setup(s => s.GetRecentAverageResponseTime("weather", It.IsAny<TimeSpan>())).Returns(160.0); // 60% increase

            var service = new PerformanceMonitorService(statsMock.Object, loggerMock.Object);
            using var cts = new CancellationTokenSource();

            // Act
            var task = service.StartAsync(cts.Token);
            await Task.Delay(50); // Allow first run of loop
            cts.Cancel();
            await task;

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PERFORMANCE ANOMALY DETECTED")),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task PerformanceMonitorService_DoesNotLogWarning_WhenRecentLatencyIsWithinNormalLimits()
        {
            // Arrange
            var statsMock = new Mock<IStatisticsService>();
            var loggerMock = new Mock<ILogger<PerformanceMonitorService>>();

            statsMock.Setup(s => s.GetStatistics()).Returns(new List<ApiStatistics>
            {
                new ApiStatistics { ServiceName = "weather" }
            });

            statsMock.Setup(s => s.GetOverallAverageResponseTime("weather")).Returns(100.0);
            statsMock.Setup(s => s.GetRecentAverageResponseTime("weather", It.IsAny<TimeSpan>())).Returns(110.0); // 10% increase

            var service = new PerformanceMonitorService(statsMock.Object, loggerMock.Object);
            using var cts = new CancellationTokenSource();

            // Act
            var task = service.StartAsync(cts.Token);
            await Task.Delay(50);
            cts.Cancel();
            await task;

            // Assert
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
    }
}
