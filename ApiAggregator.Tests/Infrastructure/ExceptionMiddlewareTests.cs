using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using ApiAggregator.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiAggregator.Tests.Infrastructure
{
    public class ExceptionMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_WhenNextDelegateSucceeds_DoesNotModifyResponse()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                return Task.CompletedTask;
            };

            var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
            Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenNextDelegateThrows_WritesInternalServerErrorResponse()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var responseStream = new MemoryStream();
            context.Response.Body = responseStream;

            var exceptionMessage = "Something went terribly wrong!";
            RequestDelegate next = (ctx) => throw new Exception(exceptionMessage);

            var middleware = new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            // Read response body
            responseStream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(responseStream);
            var responseBody = await reader.ReadToEndAsync();

            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(errorResponse);
            Assert.Equal((int)HttpStatusCode.InternalServerError, errorResponse.StatusCode);
            Assert.Equal("Internal Server Error from API Aggregation Service.", errorResponse.Message);
            Assert.Equal(exceptionMessage, errorResponse.Detailed);
        }
    }
}
