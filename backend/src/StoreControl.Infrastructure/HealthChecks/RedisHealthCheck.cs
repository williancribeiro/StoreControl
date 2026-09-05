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
        if (!options.Value.Enabled)
        {
            return HealthCheckResult.Healthy("Cache disabled.");
        }

        try
        {
            var database = await connection.GetDatabaseAsync(cancellationToken);
            await database.PingAsync().WaitAsync(
                TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds), cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return HealthCheckResult.Degraded("Redis unavailable; SQL fallback remains available.");
        }
    }
}
