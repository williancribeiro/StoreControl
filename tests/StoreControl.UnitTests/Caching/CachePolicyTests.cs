using StoreControl.Infrastructure.Caching;

namespace StoreControl.UnitTests.Caching;

public sealed class CachePolicyTests
{
    [Fact]
    public void Dashboard_scope_is_canonical_but_distinct_scopes_dates_and_requested_stores_do_not_collide()
    {
        var date = new DateOnly(2026, 9, 4);
        var variant = CacheKeys.Dashboard(date, null, [3, 1, 3]);
        Assert.Equal(variant, CacheKeys.Dashboard(date, null, [1, 3]));
        Assert.NotEqual(variant, CacheKeys.Dashboard(date, null, [1]));
        Assert.NotEqual(variant, CacheKeys.Dashboard(date.AddDays(1), null, [1, 3]));
        Assert.NotEqual(CacheKeys.Dashboard(date, null, [1]), CacheKeys.Dashboard(date, 1, [1]));
        var generation = Guid.NewGuid();
        var key = CacheKeys.Build("test:a", "dashboard", generation, variant);
        Assert.NotEqual(key, CacheKeys.Build("test:a", "dashboard", Guid.NewGuid(), variant));
        Assert.NotEqual(key, CacheKeys.Build("test:b", "dashboard", generation, variant));
    }

    [Theory]
    [InlineData(0, 300, 500)]
    [InlineData(301, 300, 500)]
    [InlineData(30, 3601, 500)]
    [InlineData(30, 300, 99)]
    [InlineData(30, 300, 5001)]
    public void Invalid_TTL_or_timeout_is_rejected(int dashboard, int categories, int timeout)
    {
        Assert.False(new CacheOptions
        {
            DashboardTtlSeconds = dashboard,
            CategoriesTtlSeconds = categories,
            TimeoutMilliseconds = timeout
        }.IsValid());
    }

    [Fact]
    public void Enabled_cache_requires_connection_and_safe_bounded_prefix()
    {
        Assert.True(new CacheOptions().IsValid());
        Assert.False(new CacheOptions { Enabled = true }.IsValid());
        Assert.False(new CacheOptions { KeyPrefix = "unsafe*prefix" }.IsValid());
        Assert.True(new CacheOptions { Enabled = true, RedisConnection = "localhost:6379" }.IsValid());
    }
}
