using System.Diagnostics;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ApiAggregator.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AggregationController : ControllerBase
    {
        private readonly IEnumerable<IExternalApiService> _apiServices;
        private readonly IStatisticsService _statisticsService;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<AggregationController> _logger;

        public AggregationController(
            IEnumerable<IExternalApiService> apiServices,
            IStatisticsService statisticsService,
            IMemoryCache memoryCache,
            ILogger<AggregationController> logger)
        {
            _apiServices = apiServices;
            _statisticsService = statisticsService;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAggregatedData([FromQuery] FilterParams filterParams, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Received request for aggregated data");

            // 1. Identify which services to query based on FilterParams.Services
            var activeServices = _apiServices.ToList();
            if (!string.IsNullOrWhiteSpace(filterParams.Services))
            {
                var requestedServices = filterParams.Services
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToList();

                activeServices = _apiServices
                    .Where(s => requestedServices.Contains(s.ServiceName.ToLowerInvariant()))
                    .ToList();
            }

            var aggregatedResult = new AggregatedResult();
            var tasks = activeServices.Select(async service =>
            {
                var stopwatch = Stopwatch.StartNew();
                bool isSuccess = false;
                string? errorMessage = null;
                object? result = null;

                // Check cache state before execution
                var cacheKey = $"cache_{service.ServiceName.ToLowerInvariant()}";
                bool isCachedBefore = _memoryCache.TryGetValue(cacheKey, out _);

                try
                {
                    result = await service.FetchDataAsync(cancellationToken);
                    isSuccess = true;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    _logger.LogError(ex, "Service '{ServiceName}' failed during aggregation", service.ServiceName);
                }
                finally
                {
                    stopwatch.Stop();
                    var elapsed = stopwatch.ElapsedMilliseconds;

                    // Log performance statistics
                    _statisticsService.RecordRequest(service.ServiceName, isSuccess, elapsed, errorMessage);

                    // Map result dynamically
                    if (isSuccess && result != null)
                    {
                        var key = service.ServiceName.ToLowerInvariant();
                        lock (aggregatedResult.Data)
                        {
                            aggregatedResult.Data[key] = result;
                        }
                    }

                    // Populate Metadata dictionary
                    lock (aggregatedResult.Metadata)
                    {
                        aggregatedResult.Metadata[service.ServiceName] = new ServiceMetadata
                        {
                            IsSuccess = isSuccess,
                            ResponseTimeMs = elapsed,
                            ErrorMessage = errorMessage,
                            IsCached = isCachedBefore
                        };
                    }
                }
            });

            // 2. Execute all calls in parallel using Task.WhenAll
            await Task.WhenAll(tasks);

            // 3. Filter results dynamically if Keyword is specified
            if (!string.IsNullOrWhiteSpace(filterParams.Keyword))
            {
                var keyword = filterParams.Keyword.Trim();
                foreach (var service in activeServices)
                {
                    var key = service.ServiceName.ToLowerInvariant();
                    if (service is IFilterableService filterableService && aggregatedResult.Data.TryGetValue(key, out var data) && data != null)
                    {
                        var filteredData = filterableService.Filter(data, keyword);
                        if (filteredData == null)
                        {
                            aggregatedResult.Data.Remove(key);
                        }
                        else
                        {
                            aggregatedResult.Data[key] = filteredData;
                        }
                    }
                }
            }

            // 4. Sort metadata according to SortBy and SortOrder
            if (!string.IsNullOrWhiteSpace(filterParams.SortBy))
            {
                var sortBy = filterParams.SortBy.ToLowerInvariant();
                var isDesc = filterParams.SortOrder?.ToLowerInvariant() == "desc";

                if (sortBy == "name")
                {
                    aggregatedResult.Metadata = isDesc
                        ? aggregatedResult.Metadata.OrderByDescending(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
                        : aggregatedResult.Metadata.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                }
                else if (sortBy == "duration")
                {
                    aggregatedResult.Metadata = isDesc
                        ? aggregatedResult.Metadata.OrderByDescending(x => x.Value.ResponseTimeMs).ToDictionary(x => x.Key, x => x.Value)
                        : aggregatedResult.Metadata.OrderBy(x => x.Value.ResponseTimeMs).ToDictionary(x => x.Key, x => x.Value);
                }
            }

            return Ok(aggregatedResult);
        }
    }
}
