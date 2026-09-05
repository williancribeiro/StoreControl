using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Application.Abstractions.Persistence;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.IntegrationTests.Support;

public sealed class SqlServerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminPassword = "StoreControl_Test_Admin_2026!";
    public const string UserPassword = "StoreControl_Test_User_2026!";

    private readonly string _connectionString = BuildConnectionString();
    public string CachePrefix { get; } = $"storecontrol:test:{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:StoreControl", _connectionString);
        builder.UseSetting(
            "Authentication:Jwt:SigningKey",
            "StoreControl_Integration_Test_Signing_Key_Only_2026!");
        builder.UseSetting("Authentication:RefreshToken:CookieSecure", "false");
        builder.UseSetting("Seed:DemoDataEnabled", "true");
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:UserPassword", UserPassword);
        var redisConnection = Environment.GetEnvironmentVariable("STORECONTROL_TEST_REDIS_CONNECTION");
        builder.UseSetting("Cache:Enabled", (!string.IsNullOrWhiteSpace(redisConnection)).ToString());
        builder.UseSetting("Cache:RedisConnection", redisConnection ?? string.Empty);
        builder.UseSetting("Cache:KeyPrefix", CachePrefix);
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        await dbContext.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>().SeedAsync(CancellationToken.None);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        if (!databaseName.StartsWith("StoreControl_IntegrationTests_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete a database outside the integration-test prefix.");
        }

        await dbContext.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    private static string BuildConnectionString()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("STORECONTROL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                "Set STORECONTROL_TEST_DB_CONNECTION to a SQL Server connection string before running integration tests.");
        }

        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"StoreControl_IntegrationTests_{Guid.NewGuid():N}",
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }
}
