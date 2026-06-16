namespace ApiAggregator.Api.Configuration
{
    public class ApiSettings
    {
        public JwtSettings Jwt { get; set; } = new();
        public Dictionary<string, ServiceApiSettings> Apis { get; set; } = new();
        public ResilienceSettings Resilience { get; set; } = new();
    }

    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpirationInMinutes { get; set; } = 60;
    }

    public class ServiceApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 5;
        public int CacheDurationSeconds { get; set; } = 60;
    }

    public class ResilienceSettings
    {
        public int RetryCount { get; set; } = 3;
        public int BackoffBaseSeconds { get; set; } = 2;
        public int BreakDurationSeconds { get; set; } = 15;
        public double FailureRatio { get; set; } = 0.5;
        public int SamplingDurationSeconds { get; set; } = 10;
    }
}
