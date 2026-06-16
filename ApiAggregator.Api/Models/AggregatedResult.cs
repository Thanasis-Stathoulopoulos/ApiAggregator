using System.Text.Json.Serialization;

namespace ApiAggregator.Api.Models
{
    public class AggregatedResult
    {
        public WeatherResult? Weather { get; set; }
        public List<NewsResult>? News { get; set; }
        public GitHubResult? GitHub { get; set; }
        public Dictionary<string, ServiceMetadata> Metadata { get; set; } = new();
    }

    public class ServiceMetadata
    {
        public bool IsSuccess { get; set; }
        public long ResponseTimeMs { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ErrorMessage { get; set; }
        public bool IsCached { get; set; }
    }
}
