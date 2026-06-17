using ApiAggregator.Api.Configuration;
using ApiAggregator.Api.Controllers;
using ApiAggregator.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiAggregator.Tests.Controllers
{
    public class AuthControllerTests
    {
        private static IOptions<ApiSettings> BuildSettings() =>
            Options.Create(new ApiSettings
            {
                Jwt = new JwtSettings
                {
                    Key = "super_secret_key_that_is_long_enough_for_hmac_sha256",
                    Issuer = "ApiAggregator",
                    Audience = "ApiAggregatorUsers",
                    ExpirationInMinutes = 60
                }
            });

        [Fact]
        public void Login_ReturnsOkWithToken_WhenCredentialsAreValid()
        {
            var controller = new AuthController(BuildSettings());
            var request = new LoginRequest { Username = "admin", Password = "password123" };

            var response = controller.Login(request);

            var okResult = Assert.IsType<OkObjectResult>(response);
            Assert.NotNull(okResult.Value);
            var tokenProp = okResult.Value.GetType().GetProperty("Token");
            Assert.NotNull(tokenProp);
            var token = tokenProp.GetValue(okResult.Value) as string;
            Assert.False(string.IsNullOrEmpty(token));
        }

        [Fact]
        public void Login_ReturnsUnauthorized_WhenCredentialsAreInvalid()
        {
            var controller = new AuthController(BuildSettings());
            var request = new LoginRequest { Username = "admin", Password = "wrongpassword" };

            var response = controller.Login(request);

            Assert.IsType<UnauthorizedObjectResult>(response);
        }
    }
}
