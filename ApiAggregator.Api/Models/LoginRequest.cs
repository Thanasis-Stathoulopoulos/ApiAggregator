namespace ApiAggregator.Api.Models
{
    /// <summary>
    /// Represents a login request.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// The username. Use "admin".
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The password. Use "password123".
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}
