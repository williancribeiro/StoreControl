using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Security;

public sealed class AuthenticationRateLimitingTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Login_rejects_requests_after_the_configured_per_ip_limit()
    {
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var rejectedLogin = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = $"unknown-{attempt}@storecontrol.local",
                password = SqlServerApiFactory.UserPassword
            });
            Assert.Equal(HttpStatusCode.Unauthorized, rejectedLogin.StatusCode);
        }

        var rateLimited = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "unknown-rate-limited@storecontrol.local",
            password = SqlServerApiFactory.UserPassword
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimited.StatusCode);
        Assert.Equal("application/problem+json", rateLimited.Content.Headers.ContentType?.MediaType);
        var payload = await rateLimited.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(
            "authentication_rate_limit_exceeded",
            payload!.RootElement.GetProperty("errorCode").GetString());
    }
}
