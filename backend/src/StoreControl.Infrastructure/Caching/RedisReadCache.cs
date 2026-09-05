using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Caching;

internal sealed class RedisReadCache(
    StoreControlDbContext dbContext,
    RedisConnection connection,
    IOptions<CacheOptions> options,
    CacheTelemetry telemetry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public bool Enabled => options.Value.Enabled;

    public async Task<T> GetOrCreateAsync<T>(
        string region, string variant, int ttlSeconds,
        Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enabled)
        {
            telemetry.Record(region, "disabled");
            return await load(cancellationToken);
        }

        // A committed SQL generation makes invalidation durable, even while Redis is offline.
        var generation = await dbContext.CacheRevisions.AsNoTracking()
            .Where(revision => revision.Name == region)
            .Select(revision => revision.Generation)
            .SingleAsync(cancellationToken);
        var key = CacheKeys.Build(options.Value.KeyPrefix, region, generation, variant);
        IDatabase? redis = null;
        var started = Stopwatch.GetTimestamp();
        using (var activity = CacheTelemetry.Activities.StartActivity("cache.get", ActivityKind.Client))
        {
            activity?.SetTag("cache.region", region);
            try
            {
                redis = await connection.GetDatabaseAsync(cancellationToken);
                var payload = await redis.StringGetAsync(key).WaitAsync(
                    TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds), cancellationToken);
                if (payload.HasValue)
                {
                    var cached = JsonSerializer.Deserialize<T>(payload.ToString(), JsonOptions);
                    if (cached is not null)
                    {
                        telemetry.Available();
                        telemetry.Record(region, "hit");
                        activity?.SetTag("cache.outcome", "hit");
                        return cached;
                    }

                    telemetry.Record(region, "corrupt");
                }
            }
            catch (JsonException)
            {
                telemetry.Record(region, "corrupt");
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                telemetry.Unavailable(exception);
                telemetry.Record(region, "fallback");
                activity?.SetStatus(ActivityStatusCode.Error);
                redis = null;
            }
            finally
            {
                telemetry.RecordDuration("get", started);
            }
        }

        if (redis is not null)
        {
            telemetry.Record(region, "miss");
        }

        // SQL/domain exceptions and cancellation are intentionally outside Redis fallback handling.
        var result = await load(cancellationToken);
        if (redis is null)
        {
            return result;
        }

        var serialized = JsonSerializer.Serialize(result, JsonOptions);
        started = Stopwatch.GetTimestamp();
        using var setActivity = CacheTelemetry.Activities.StartActivity("cache.set", ActivityKind.Client);
        setActivity?.SetTag("cache.region", region);
        try
        {
            await redis.StringSetAsync(key, serialized, TimeSpan.FromSeconds(ttlSeconds))
                .WaitAsync(TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds), cancellationToken);
            telemetry.Available();
            telemetry.Record(region, "fill");
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            telemetry.Unavailable(exception);
            telemetry.Record(region, "write_failure");
            setActivity?.SetStatus(ActivityStatusCode.Error);
        }
        finally
        {
            telemetry.RecordDuration("set", started);
        }

        return result;
    }
}
