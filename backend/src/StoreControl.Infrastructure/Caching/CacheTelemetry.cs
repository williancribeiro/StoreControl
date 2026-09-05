using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace StoreControl.Infrastructure.Caching;

public sealed class CacheTelemetry(ILogger<CacheTelemetry> logger)
{
    public const string SourceName = "StoreControl.Cache";
    internal static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("storecontrol.cache.requests");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("storecontrol.cache.redis.duration", "ms");
    private int _unavailable;

    internal void Record(string region, string outcome)
    {
        Requests.Add(1, new("cache.region", region), new("cache.outcome", outcome));
        logger.LogDebug("Cache {CacheRegion}: {CacheOutcome}", region, outcome);
    }

    internal void RecordDuration(string operation, long started) =>
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, new KeyValuePair<string, object?>("cache.operation", operation));

    internal void Unavailable(Exception exception)
    {
        if (Interlocked.Exchange(ref _unavailable, 1) == 0)
        {
            logger.LogWarning("Redis cache unavailable ({FailureType}); reads fall back to SQL Server", exception.GetType().Name);
        }
    }

    internal void Available()
    {
        if (Interlocked.Exchange(ref _unavailable, 0) == 1)
        {
            logger.LogInformation("Redis cache connection recovered");
        }
    }
}
