using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StoreControl.Infrastructure.Caching;

namespace StoreControl.Infrastructure.HealthChecks;

internal sealed class RedisHealthCheck(RedisConnection connection, IOptions<CacheOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var telemetry = DependencyHealthTelemetry.Start("redis");
        if (!options.Value.Enabled)
        {
            var disabled = HealthCheckResult.Healthy("Cache disabled.");
            telemetry.Complete(disabled.Status);
            return disabled;
        }

        try
        {
            var database = await connection.GetDatabaseAsync(cancellationToken);
            await database.PingAsync().WaitAsync(
                TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds), cancellationToken);
            var health = HealthCheckResult.Healthy();
            telemetry.Complete(health.Status);
            return health;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            var health = HealthCheckResult.Degraded("Redis unavailable; SQL fallback remains available.");
            telemetry.Complete(health.Status);
            return health;
        }
    }
}
