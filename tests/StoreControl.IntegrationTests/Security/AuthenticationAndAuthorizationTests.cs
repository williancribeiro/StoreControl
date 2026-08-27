using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Security;

public sealed class AuthenticationAndAuthorizationTests(SqlServerApiFactory factory)
    : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Login_returns_access_token_and_http_only_refresh_cookie()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var response = await LoginAsync(
            client,
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.False(string.IsNullOrWhiteSpace(payload!.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal(
            "admin@storecontrol.local",
            payload.RootElement.GetProperty("user").GetProperty("email").GetString());
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains(".StoreControl.RefreshToken=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_problem_details_401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stores");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Seller_cannot_create_store_but_administrator_can()
    {
        using var client = factory.CreateClient();
        var sellerToken = await GetAccessTokenAsync(
            client,
            "vendedor@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sellerToken);

        var forbidden = await client.PostAsJsonAsync(
            "/api/stores",
            new { code = "BLOCKED", name = "Should Not Exist" });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var administratorToken = await GetAccessTokenAsync(
            client,
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", administratorToken);
        var created = await client.PostAsJsonAsync(
            "/api/stores",
            new { code = "TESTE", name = "Loja de Integração" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_replay_revokes_the_token_family()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var login = await LoginAsync(
            client,
            "gerente@storecontrol.local",
            SqlServerApiFactory.UserPassword);
        var originalCookie = ExtractCookie(login);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", originalCookie);
        var refresh = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotatedCookie = ExtractCookie(refresh);
        Assert.NotEqual(originalCookie, rotatedCookie);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replayRequest.Headers.Add("Cookie", originalCookie);
        var replay = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        using var familyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        familyRequest.Headers.Add("Cookie", rotatedCookie);
        var revokedFamily = await client.SendAsync(familyRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamily.StatusCode);
    }

    [Fact]
    public async Task Disabling_user_invalidates_existing_access_token()
    {
        using var client = factory.CreateClient();
        var administratorToken = await GetAccessTokenAsync(
            client,
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", administratorToken);

        var roles = await client.GetFromJsonAsync<JsonDocument>("/api/roles");
        var sellerRole = roles!.RootElement.EnumerateArray()
            .Single(role => role.GetProperty("name").GetString() == "Vendedor");
        var stores = await client.GetFromJsonAsync<JsonDocument>("/api/stores");
        var store = stores!.RootElement.EnumerateArray().First();
        var email = $"security-test-{Guid.NewGuid():N}@storecontrol.local";
        var create = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Usuário de Segurança",
            email,
            password = SqlServerApiFactory.UserPassword,
            isAdministrator = false,
            storeAssignments = new[]
            {
                new
                {
                    storeId = store.GetProperty("id").GetInt32(),
                    roleId = sellerRole.GetProperty("id").GetGuid()
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdUser = await create.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = createdUser!.RootElement.GetProperty("id").GetGuid();

        var userToken = await GetAccessTokenAsync(client, email, SqlServerApiFactory.UserPassword);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", administratorToken);
        var update = await client.PutAsJsonAsync($"/api/users/{userId}", new
        {
            fullName = "Usuário de Segurança",
            isActive = false,
            isAdministrator = false,
            storeAssignments = new[]
            {
                new
                {
                    storeId = store.GetProperty("id").GetInt32(),
                    roleId = sellerRole.GetProperty("id").GetGuid()
                }
            }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var afterDeactivation = await client.GetAsync("/api/stores");
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivation.StatusCode);
    }

    [Fact]
    public async Task Store_update_rejects_stale_rowversion()
    {
        using var client = factory.CreateClient();
        var administratorToken = await GetAccessTokenAsync(
            client,
            "admin@storecontrol.local",
            SqlServerApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", administratorToken);
        var code = $"RV{Guid.NewGuid():N}"[..18];
        var create = await client.PostAsJsonAsync(
            "/api/stores",
            new { code, name = "Loja Concorrência" });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonDocument>();
        var store = created!.RootElement;
        var storeId = store.GetProperty("id").GetInt32();
        var rowVersion = store.GetProperty("rowVersion").GetString();

        var firstUpdate = await client.PutAsJsonAsync($"/api/stores/{storeId}", new
        {
            code,
            name = "Loja Concorrência A",
            isActive = true,
            rowVersion
        });
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        var staleUpdate = await client.PutAsJsonAsync($"/api/stores/{storeId}", new
        {
            code,
            name = "Loja Concorrência B",
            isActive = true,
            rowVersion
        });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private static async Task<string> GetAccessTokenAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await LoginAsync(client, email, password);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return payload!.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static string ExtractCookie(HttpResponseMessage response)
    {
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        return setCookie.Split(';', 2)[0];
    }
}
