using System.Text.Json.Serialization;

namespace ApiAggregator.Api.Models
{
    public class AggregatedResult
    {
        [JsonIgnore]
        public WeatherResult? Weather
        {
            get => Data.TryGetValue("weather", out var val) ? val as WeatherResult : null;
            set { if (value != null) Data["weather"] = value; else Data.Remove("weather"); }
        }

        [JsonIgnore]
        public List<NewsResult>? News
        {
            get => Data.TryGetValue("news", out var val) ? val as List<NewsResult> : null;
            set { if (value != null) Data["news"] = value; else Data.Remove("news"); }
        }

        [JsonIgnore]
        public GitHubResult? GitHub
        {
            get => Data.TryGetValue("github", out var val) ? val as GitHubResult : null;
            set { if (value != null) Data["github"] = value; else Data.Remove("github"); }
        }

        [JsonExtensionData]
        public Dictionary<string, object> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ServiceMetadata> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
