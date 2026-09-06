using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StoreControl.Application.Abstractions.Persistence;
using StoreControl.Infrastructure;
using StoreControl.Infrastructure.Messaging;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.IntegrationTests.Support;

public sealed class RabbitMqApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminPassword = "StoreControl_Test_Admin_2026!";

    private readonly string _connectionString = BuildConnectionString();
    private readonly RabbitSettings _rabbit = RabbitSettings.FromEnvironment();

    public string TopologyPrefix { get; } = $"storecontrol.tests.{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:StoreControl", _connectionString);
        builder.UseSetting("Authentication:Jwt:SigningKey", "StoreControl_Rabbit_Test_Signing_Key_Only_2026!");
        builder.UseSetting("Authentication:RefreshToken:CookieSecure", "false");
        builder.UseSetting("Seed:DemoDataEnabled", "true");
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:UserPassword", "StoreControl_Test_User_2026!");
        builder.UseSetting("Cache:Enabled", "false");
        builder.UseSetting("Messaging:Enabled", "true");
        builder.UseSetting("Messaging:HostName", _rabbit.Host);
        builder.UseSetting("Messaging:Port", _rabbit.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Messaging:UserName", _rabbit.UserName);
        builder.UseSetting("Messaging:Password", _rabbit.Password);
        builder.UseSetting("Messaging:VirtualHost", _rabbit.VirtualHost);
        builder.UseSetting("Messaging:TopologyPrefix", TopologyPrefix);
        builder.UseSetting("Messaging:ConnectionTimeoutMilliseconds", "2000");
        builder.UseSetting("Messaging:RecoveryIntervalSeconds", "1");
        builder.UseSetting("Messaging:OutboxPollingMilliseconds", "100");
        builder.UseSetting("Messaging:OutboxBatchSize", "5");
        builder.UseSetting("Messaging:OutboxLeaseSeconds", "10");
        builder.UseSetting("Messaging:ConsumerPrefetch", "1");
        builder.UseSetting("Messaging:RetryDelaysMilliseconds:0", "200");
        builder.UseSetting("Messaging:RetryDelaysMilliseconds:1", "400");
        builder.UseSetting("Messaging:RetryDelaysMilliseconds:2", "800");
        builder.ConfigureServices(services => services.AddSaleFinalizedConsumer());
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
        await CleanupTopologyAsync();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        if (!databaseName.StartsWith("StoreControl_RabbitIntegrationTests_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete a database outside the RabbitMQ test prefix.");
        }

        await dbContext.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    private async Task CleanupTopologyAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetRequiredService<RabbitMqConnection>();
        var topology = scope.ServiceProvider.GetRequiredService<RabbitTopology>();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessagingOptions>>().Value;
        var rabbitConnection = await connection.GetAsync(default);
        await using var channel = await rabbitConnection.CreateChannelAsync();
        await channel.QueueDeleteAsync(topology.SaleFinalizedQueue, ifUnused: false, ifEmpty: false);
        await channel.QueueDeleteAsync(topology.SaleFinalizedDeadLetterQueue, ifUnused: false, ifEmpty: false);
        for (var index = 0; index < options.RetryDelaysMilliseconds.Length; index++)
        {
            await channel.QueueDeleteAsync(topology.RetryQueue(index), ifUnused: false, ifEmpty: false);
        }

        await channel.ExchangeDeleteAsync(topology.EventsExchange, ifUnused: false);
        await channel.ExchangeDeleteAsync(topology.RetryExchange, ifUnused: false);
        await channel.ExchangeDeleteAsync(topology.DeadLetterExchange, ifUnused: false);
    }

    private static string BuildConnectionString()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("STORECONTROL_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                "Set STORECONTROL_TEST_DB_CONNECTION before running RabbitMQ integration tests.");
        }

        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"StoreControl_RabbitIntegrationTests_{Guid.NewGuid():N}",
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }

    private sealed record RabbitSettings(string Host, int Port, string UserName, string Password, string VirtualHost)
    {
        public static RabbitSettings FromEnvironment()
        {
            static string Required(string name) =>
                Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
                    ? value
                    : throw new InvalidOperationException($"Set {name} before running RabbitMQ integration tests.");

            var portText = Required("STORECONTROL_TEST_RABBITMQ_PORT");
            if (!int.TryParse(portText, out var port) || port is < 1 or > 65_535)
            {
                throw new InvalidOperationException("STORECONTROL_TEST_RABBITMQ_PORT is invalid.");
            }

            return new RabbitSettings(
                Required("STORECONTROL_TEST_RABBITMQ_HOST"),
                port,
                Required("STORECONTROL_TEST_RABBITMQ_USERNAME"),
                Required("STORECONTROL_TEST_RABBITMQ_PASSWORD"),
                Environment.GetEnvironmentVariable("STORECONTROL_TEST_RABBITMQ_VHOST") ?? "/");
        }
    }
}
