using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiAggregator.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiAggregator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApiSettings _settings;

        public AuthController(IOptions<ApiSettings> settings)
        {
            _settings = settings.Value;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request.Username == "admin" && request.Password == "password123")
            {
                var token = GenerateJwtToken(request.Username);
                return Ok(new { Token = token });
            }

            return Unauthorized("Invalid credentials. Use admin / password123.");
        }

        private string GenerateJwtToken(string username)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_settings.Jwt.Key);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, username)
                }),
                Expires = DateTime.UtcNow.AddMinutes(_settings.Jwt.ExpirationInMinutes),
                Issuer = _settings.Jwt.Issuer,
                Audience = _settings.Jwt.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
