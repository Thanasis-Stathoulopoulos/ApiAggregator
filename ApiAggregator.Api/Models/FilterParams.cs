namespace ApiAggregator.Api.Models
{
    public class FilterParams
    {
        public string? Services { get; set; }
        public string? Keyword { get; set; }
        public string? SortBy { get; set; }
        public string? SortOrder { get; set; } = "asc";
    }
}
