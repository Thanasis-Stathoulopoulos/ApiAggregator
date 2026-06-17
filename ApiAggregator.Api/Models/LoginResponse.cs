namespace ApiAggregator.Api.Models
{
    /// <summary>
    /// Represents the login response containing the JWT access token.
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// The JWT access token.
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }
}
