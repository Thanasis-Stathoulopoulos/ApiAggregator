namespace ApiAggregator.Api.Models
{
    public class NewsResult
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Score { get; set; }
        public DateTime PublishedAt { get; set; }
    }
}
