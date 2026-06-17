using ApiAggregator.Api.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace ApiAggregator.Api.Infrastructure
{
    public interface IResiliencePolicies
    {
        ResiliencePipeline GetPipeline(string serviceName);
    }

    public class ResiliencePolicies : IResiliencePolicies
    {
        private readonly ApiSettings _settings;
        private readonly ILogger<ResiliencePolicies> _logger;
        private readonly Dictionary<string, ResiliencePipeline> _pipelines = new();

        public ResiliencePolicies(IOptions<ApiSettings> settings, ILogger<ResiliencePolicies> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            InitializePipelines();
        }

        private void InitializePipelines()
        {
            var res = _settings.Resilience;
            foreach (var key in _settings.Apis.Keys)
            {
                var serviceSettings = _settings.Apis[key];
                var serviceName = key.ToLowerInvariant();

                var pipeline = new ResiliencePipelineBuilder()
                    .AddTimeout(TimeSpan.FromSeconds(serviceSettings.TimeoutSeconds))
                    .AddRetry(new RetryStrategyOptions
                    {
                        ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                        MaxRetryAttempts = res.RetryCount,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        Delay = TimeSpan.FromSeconds(res.BackoffBaseSeconds),
                        OnRetry = args =>
                        {
                            _logger.LogWarning("Retry attempt {Attempt} for service {Service} due to: {Error}", 
                                args.AttemptNumber + 1, serviceName, args.Outcome.Exception?.Message);
                            return default;
                        }
                    })
                    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                    {
                        ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                        FailureRatio = res.FailureRatio,
                        SamplingDuration = TimeSpan.FromSeconds(res.SamplingDurationSeconds),
                        MinimumThroughput = 5,
                        BreakDuration = TimeSpan.FromSeconds(res.BreakDurationSeconds),
                        OnOpened = args =>
                        {
                            _logger.LogCritical("Circuit opened for service {Service} for {BreakDuration}s. Root Cause: {Error}",
                                serviceName, res.BreakDurationSeconds, args.Outcome.Exception?.Message);
                            return default;
                        },
                        OnClosed = args =>
                        {
                            _logger.LogInformation("Circuit closed (recovered) for service {Service}.", serviceName);
                            return default;
                        }
                    })
                    .Build();

                _pipelines[serviceName] = pipeline;
            }
        }

        public ResiliencePipeline GetPipeline(string serviceName)
        {
            if (_pipelines.TryGetValue(serviceName.ToLowerInvariant(), out var pipeline))
            {
                return pipeline;
            }

            // Fallback default pipeline
            return new ResiliencePipelineBuilder()
                .AddTimeout(TimeSpan.FromSeconds(5))
                .Build();
        }
    }
}
