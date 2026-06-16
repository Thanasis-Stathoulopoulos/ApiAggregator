namespace ApiAggregator.Api.Models
{
    public class GitHubResult
    {
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public int PublicRepos { get; set; }
        public int Followers { get; set; }
        public int Following { get; set; }
        public string HtmlUrl { get; set; } = string.Empty;
    }
}
