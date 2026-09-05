using System.Text.RegularExpressions;
using StackExchange.Redis;

namespace StoreControl.Infrastructure.Caching;

internal sealed class CacheOptions
{
    public bool Enabled { get; set; }
    public string RedisConnection { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "storecontrol:local";
    public int DashboardTtlSeconds { get; set; } = 30;
    public int CategoriesTtlSeconds { get; set; } = 300;
    public int TimeoutMilliseconds { get; set; } = 500;

    public bool IsValid() =>
        (!Enabled || HasValidConnection()) &&
        Regex.IsMatch(KeyPrefix, "^[a-zA-Z0-9:_-]{1,80}$") &&
        DashboardTtlSeconds is >= 1 and <= 300 &&
        CategoriesTtlSeconds is >= 1 and <= 3600 &&
        TimeoutMilliseconds is >= 100 and <= 5000;

    private bool HasValidConnection()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(RedisConnection) &&
                ConfigurationOptions.Parse(RedisConnection).EndPoints.Count > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return false;
        }
    }
}
