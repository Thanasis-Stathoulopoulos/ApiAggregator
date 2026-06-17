using System.ComponentModel.DataAnnotations;

namespace ApiAggregator.Api.Models
{
    public class FilterParams
    {
        [MaxLength(200)]
        public string? Services { get; set; }

        [MaxLength(100)]
        public string? Keyword { get; set; }

        [RegularExpression("(?i)^(name|duration)$", ErrorMessage = "SortBy must be 'name' or 'duration'.")]
        public string? SortBy { get; set; }

        [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "SortOrder must be 'asc' or 'desc'.")]
        public string? SortOrder { get; set; } = "asc";
    }
}
