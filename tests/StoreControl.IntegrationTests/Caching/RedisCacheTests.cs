using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StoreControl.Infrastructure.Caching;
using StoreControl.Infrastructure.Persistence;
using StoreControl.IntegrationTests.Support;

namespace StoreControl.IntegrationTests.Caching;

public sealed class RedisCacheTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    [Fact]
    public async Task Real_Redis_hits_misses_expires_and_emits_metrics_and_traces()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var cache = RequiredCache(scope.ServiceProvider);
        var events = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CacheTelemetry.SourceName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "cache.outcome") events.Add(tag.Value!.ToString()!);
        });
        listener.Start();
        var spans = new List<string>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CacheTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => spans.Add(activity.OperationName)
        };
        ActivitySource.AddActivityListener(activityListener);
        var variant = Guid.NewGuid().ToString();
        var loads = 0;
        Task<Probe> Load(CancellationToken _) => Task.FromResult(new Probe($"load-{++loads}"));
        Assert.Equal("load-1", (await cache.GetOrCreateAsync("categories", variant, 1, Load, default)).Value);
        Assert.Equal("load-1", (await cache.GetOrCreateAsync("categories", variant, 1, Load, default)).Value);
        var redis = await scope.ServiceProvider.GetRequiredService<RedisConnection>().GetDatabaseAsync(default);
        var key = await KeyAsync(scope.ServiceProvider, "categories", variant);
        var ttl = await redis.KeyTimeToLiveAsync(key);
        Assert.True(ttl > TimeSpan.Zero && ttl <= TimeSpan.FromSeconds(1));
        await Task.Delay(1200);
        Assert.Equal("load-2", (await cache.GetOrCreateAsync("categories", variant, 1, Load, default)).Value);
        Assert.Equal(2, loads);
        Assert.Contains("hit", events);
        Assert.Contains("miss", events);
        Assert.Contains("fill", events);
        Assert.Contains("cache.get", spans);
        Assert.Contains("cache.set", spans);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Corrupt_payload_is_replaced_by_a_real_load(string payload)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var cache = RequiredCache(scope.ServiceProvider);
        var variant = Guid.NewGuid().ToString();
        var redis = await scope.ServiceProvider.GetRequiredService<RedisConnection>().GetDatabaseAsync(default);
        var key = await KeyAsync(scope.ServiceProvider, "categories", variant);
        await redis.StringSetAsync(key, payload, TimeSpan.FromSeconds(10));
        var result = await cache.GetOrCreateAsync("categories", variant, 10,
            _ => Task.FromResult(new Probe("repaired")), default);
        Assert.Equal("repaired", result.Value);
        Assert.Contains("repaired", (await redis.StringGetAsync(key)).ToString());
    }

    [Fact]
    public async Task Slow_fill_cannot_repopulate_the_generation_created_by_a_concurrent_commit()
    {
        await using var oldScope = factory.Services.CreateAsyncScope();
        var oldCache = RequiredCache(oldScope.ServiceProvider);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var variant = Guid.NewGuid().ToString();
        var pending = oldCache.GetOrCreateAsync("categories", variant, 30, async token =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return new Probe("before commit");
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var newScope = factory.Services.CreateAsyncScope();
            var db = newScope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
            await db.Database.ExecuteSqlRawAsync("UPDATE TOP (1) [catalog].[Categories] SET [Name] = [Name]");
            var newCache = RequiredCache(newScope.ServiceProvider);
            Assert.Equal("after commit", (await newCache.GetOrCreateAsync("categories", variant, 30,
                _ => Task.FromResult(new Probe("after commit")), default)).Value);
            release.SetResult();
            Assert.Equal("before commit", (await pending).Value);
            Assert.Equal("after commit", (await newCache.GetOrCreateAsync<Probe>("categories", variant, 30,
                _ => throw new InvalidOperationException("Must hit the new generation"), default)).Value);
        }
        finally
        {
            release.TrySetResult();
            await pending;
        }
    }

    [Fact]
    public async Task Offline_Redis_falls_back_and_a_write_cannot_resurrect_old_cached_data()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var online = RequiredCache(services);
        var variant = Guid.NewGuid().ToString();
        await online.GetOrCreateAsync("categories", variant, 30, _ => Task.FromResult(new Probe("old")), default);
        var unusedPort = new TcpListener(IPAddress.Loopback, 0);
        unusedPort.Start();
        var port = ((IPEndPoint)unusedPort.LocalEndpoint).Port;
        unusedPort.Stop();
        var options = Options.Create(new CacheOptions
        {
            Enabled = true,
            RedisConnection = $"127.0.0.1:{port}",
            KeyPrefix = factory.CachePrefix,
            TimeoutMilliseconds = 100
        });
        await using var disconnected = new RedisConnection(options);
        var db = services.GetRequiredService<StoreControlDbContext>();
        var offline = new RedisReadCache(db, disconnected, options, services.GetRequiredService<CacheTelemetry>());
        await db.Database.ExecuteSqlRawAsync("UPDATE TOP (1) [catalog].[Categories] SET [Name] = [Name]");
        Assert.Equal("current SQL", (await offline.GetOrCreateAsync("categories", variant, 30,
            _ => Task.FromResult(new Probe("current SQL")), default)).Value);
        Assert.Equal("current SQL", (await online.GetOrCreateAsync("categories", variant, 30,
            _ => Task.FromResult(new Probe("current SQL")), default)).Value);
    }

    [Fact]
    public async Task Failed_or_cancelled_source_load_is_not_cached_and_disabled_cache_does_not_connect()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var cache = RequiredCache(services);
        var variant = Guid.NewGuid().ToString();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync<Probe>(
            "categories", variant, 30, _ => throw new InvalidOperationException("SQL failure"), default));
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetOrCreateAsync<Probe>(
            "categories", variant, 30, token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new Probe("must not persist"));
            }, cancellation.Token));
        var redis = await services.GetRequiredService<RedisConnection>().GetDatabaseAsync(default);
        Assert.False(await redis.KeyExistsAsync(await KeyAsync(services, "categories", variant)));
        var disabledOptions = Options.Create(new CacheOptions());
        await using var disabledConnection = new RedisConnection(disabledOptions);
        var disabled = new RedisReadCache(services.GetRequiredService<StoreControlDbContext>(),
            disabledConnection, disabledOptions, services.GetRequiredService<CacheTelemetry>());
        Assert.Equal("direct", (await disabled.GetOrCreateAsync("not-a-region", variant, 30,
            _ => Task.FromResult(new Probe("direct")), default)).Value);
    }

    [Fact]
    public async Task HTTP_fallback_keeps_SQL_readiness_and_reflects_committed_writes_after_recovery()
    {
        using var admin = await LoginAsync("admin", SqlServerApiFactory.AdminPassword);
        const string path = "/api/categories?pageSize=100&sortBy=code";
        var original = await admin.GetFromJsonAsync<JsonElement>(path);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var unavailableApi = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<CacheOptions>(options =>
            {
                options.RedisConnection = $"127.0.0.1:{port}";
                options.TimeoutMilliseconds = 100;
            })));
        using var offline = unavailableApi.CreateClient();
        offline.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
        var health = await offline.GetFromJsonAsync<JsonElement>("/health/cache");
        Assert.Equal("Degraded", health.GetProperty("status").GetString());
        (await offline.GetAsync("/health/ready")).EnsureSuccessStatusCode();
        (await offline.GetAsync("/api/dashboard")).EnsureSuccessStatusCode();
        var created = await offline.PostAsJsonAsync("/api/categories", new { code = "CACHE-OFFLINE", name = "Created during outage" });
        created.EnsureSuccessStatusCode();
        var during = await offline.GetFromJsonAsync<JsonElement>(path);
        var recovered = await admin.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(original.GetProperty("totalCount").GetInt32() + 1, during.GetProperty("totalCount").GetInt32());
        Assert.Equal(during.GetProperty("totalCount").GetInt32(), recovered.GetProperty("totalCount").GetInt32());
        Assert.Contains(recovered.GetProperty("items").EnumerateArray(), item => item.GetProperty("code").GetString() == "CACHE-OFFLINE");
    }

    [Fact]
    public async Task Redis_write_rejection_does_not_discard_a_successful_SQL_result()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        RequiredCache(services);
        var redis = await services.GetRequiredService<RedisConnection>().GetDatabaseAsync(default);
        var username = $"cache-test-{Guid.NewGuid():N}";
        var password = Guid.NewGuid().ToString("N");
        var configured = services.GetRequiredService<IOptions<CacheOptions>>().Value;
        await redis.ExecuteAsync("ACL", "SETUSER", username, "reset", "on", $">{password}",
            $"~{factory.CachePrefix}:*", "+@all", "-@dangerous", "-@write");
        try
        {
            var configuration = ConfigurationOptions.Parse(configured.RedisConnection);
            configuration.User = username;
            configuration.Password = password;
            var options = Options.Create(new CacheOptions
            {
                Enabled = true,
                KeyPrefix = factory.CachePrefix,
                RedisConnection = configuration.ToString(includePassword: true)
            });
            await using var restrictedConnection = new RedisConnection(options);
            var db = services.GetRequiredService<StoreControlDbContext>();
            var cache = new RedisReadCache(db, restrictedConnection, options, services.GetRequiredService<CacheTelemetry>());
            var variant = Guid.NewGuid().ToString();
            var loads = 0;
            async Task<Probe> Load(CancellationToken token)
            {
                loads++;
                return new Probe((await db.Categories.CountAsync(token)).ToString());
            }

            var first = await cache.GetOrCreateAsync("categories", variant, 30, Load, default);
            var second = await cache.GetOrCreateAsync("categories", variant, 30, Load, default);
            Assert.Equal(first, second);
            Assert.Equal(2, loads);
            Assert.False(await redis.KeyExistsAsync(await KeyAsync(services, "categories", variant)));
        }
        finally
        {
            await redis.ExecuteAsync("ACL", "DELUSER", username);
        }
    }

    [Fact]
    public async Task Category_HTTP_lists_invalidate_and_keep_filters_and_pagination_separate()
    {
        using var client = await LoginAsync("admin", SqlServerApiFactory.AdminPassword);
        const string path = "/api/categories?page=1&pageSize=100&sortBy=code&sortDirection=asc";
        var before = await client.GetFromJsonAsync<JsonElement>(path);
        await client.GetAsync(path);
        var created = await client.PostAsJsonAsync("/api/categories", new { code = "CACHE-HTTP", name = "Cached category" });
        created.EnsureSuccessStatusCode();
        var category = await created.Content.ReadFromJsonAsync<JsonElement>();
        var after = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(before.GetProperty("totalCount").GetInt32() + 1, after.GetProperty("totalCount").GetInt32());
        var updated = await client.PutAsJsonAsync($"/api/categories/{category.GetProperty("id").GetInt32()}", new
        {
            code = "CACHE-HTTP",
            name = "Updated cached category",
            isActive = false,
            rowVersion = category.GetProperty("rowVersion").GetString()
        });
        updated.EnsureSuccessStatusCode();
        var inactive = await client.GetFromJsonAsync<JsonElement>(path + "&isActive=false");
        Assert.Contains(inactive.GetProperty("items").EnumerateArray(), item => item.GetProperty("code").GetString() == "CACHE-HTTP");
        var active = await client.GetFromJsonAsync<JsonElement>(path + "&isActive=true");
        Assert.DoesNotContain(active.GetProperty("items").EnumerateArray(), item => item.GetProperty("code").GetString() == "CACHE-HTTP");
        var first = await client.GetFromJsonAsync<JsonElement>("/api/categories?page=1&pageSize=1&sortBy=code");
        var second = await client.GetFromJsonAsync<JsonElement>("/api/categories?page=2&pageSize=1&sortBy=code");
        Assert.NotEqual(first.GetProperty("items")[0].GetProperty("id").GetInt32(), second.GetProperty("items")[0].GetProperty("id").GetInt32());
        var searched = await client.GetFromJsonAsync<JsonElement>(path + "&search=CACHE-HTTP");
        Assert.Equal("Updated cached category", Assert.Single(searched.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Minimum_stock_update_invalidates_a_warm_dashboard_and_product_changes_refresh_labels()
    {
        using var client = await LoginAsync("admin", SqlServerApiFactory.AdminPassword);
        await using var scope = factory.Services.CreateAsyncScope();
        RequiredCache(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var balance = await db.StockBalances.AsNoTracking().OrderBy(row => row.QuantityOnHand).FirstAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [inventory].[StockBalances] SET [MinimumQuantity] = 0 WHERE [StoreId] = {balance.StoreId} AND [ProductId] = {balance.ProductId}");
        var path = $"/api/dashboard?storeId={balance.StoreId}";
        var before = await client.GetFromJsonAsync<JsonElement>(path);
        var position = await client.GetFromJsonAsync<JsonElement>($"/api/inventory/balances/{balance.StoreId}/{balance.ProductId}");
        var updated = await client.PutAsJsonAsync($"/api/inventory/balances/{balance.StoreId}/{balance.ProductId}/minimum", new
        {
            minimumQuantity = balance.QuantityOnHand + 1,
            rowVersion = position.GetProperty("rowVersion").GetString()
        });
        updated.EnsureSuccessStatusCode();
        var after = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(before.GetProperty("lowStockCount").GetInt32() + 1, after.GetProperty("lowStockCount").GetInt32());
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [catalog].[Products] SET [Name] = N'Cache refreshed product' WHERE [Id] = {balance.ProductId}");
        var renamed = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Contains(renamed.GetProperty("lowStockItems").EnumerateArray(), item =>
            item.GetProperty("productId").GetInt32() == balance.ProductId &&
            item.GetProperty("productName").GetString() == "Cache refreshed product");
    }

    [Fact]
    public async Task Warm_dashboard_still_enforces_permission_and_current_store_scope()
    {
        using var admin = await LoginAsync("admin", SqlServerApiFactory.AdminPassword);
        using var manager = await LoginAsync("gerente", SqlServerApiFactory.UserPassword);
        using var clerk = await LoginAsync("estoquista", SqlServerApiFactory.UserPassword);
        await using var scope = factory.Services.CreateAsyncScope();
        RequiredCache(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();
        var south = await db.Stores.Where(store => store.Code == "SUL").Select(store => store.Id).SingleAsync();
        (await admin.GetAsync($"/api/dashboard?storeId={south}")).EnsureSuccessStatusCode();
        (await admin.GetAsync("/api/dashboard")).EnsureSuccessStatusCode();
        (await manager.GetAsync("/api/dashboard")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/dashboard?storeId={south}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clerk.GetAsync("/api/dashboard")).StatusCode);
        var managerId = await db.Users.Where(user => user.Email == "gerente@storecontrol.local").Select(user => user.Id).SingleAsync();
        await db.UserStoreRoles.Where(assignment => assignment.UserId == managerId).ExecuteDeleteAsync();
        var restricted = await manager.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.Equal(0, restricted.GetProperty("lowStockCount").GetInt32());
        Assert.Empty(restricted.GetProperty("lowStockItems").EnumerateArray());
        Assert.Equal(0m, restricted.GetProperty("revenue").GetDecimal());
    }

    private static RedisReadCache RequiredCache(IServiceProvider services)
    {
        var cache = services.GetRequiredService<RedisReadCache>();
        Assert.True(cache.Enabled, "Set STORECONTROL_TEST_REDIS_CONNECTION to a real Redis server; these tests must not silently skip Redis.");
        return cache;
    }

    private static async Task<string> KeyAsync(IServiceProvider services, string region, string variant)
    {
        var db = services.GetRequiredService<StoreControlDbContext>();
        var generation = await db.CacheRevisions.Where(row => row.Name == region).Select(row => row.Generation).SingleAsync();
        return CacheKeys.Build(services.GetRequiredService<IOptions<CacheOptions>>().Value.KeyPrefix, region, generation, variant);
    }

    private async Task<HttpClient> LoginAsync(string user, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = $"{user}@storecontrol.local", password });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.GetProperty("accessToken").GetString());
        return client;
    }

    private sealed record Probe(string Value);
}
