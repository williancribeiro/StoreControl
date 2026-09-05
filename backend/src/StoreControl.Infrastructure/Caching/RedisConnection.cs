using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace StoreControl.Infrastructure.Caching;

internal sealed class RedisConnection(IOptions<CacheOptions> options) : IAsyncDisposable
{
    private readonly object _sync = new();
    private Task<ConnectionMultiplexer>? _connection;

    public async Task<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        Task<ConnectionMultiplexer> connection;
        lock (_sync)
        {
            if (_connection is null || _connection.IsFaulted || _connection.IsCanceled)
            {
                var configuration = ConfigurationOptions.Parse(options.Value.RedisConnection);
                configuration.AbortOnConnectFail = false;
                configuration.ConnectRetry = 0;
                configuration.ConnectTimeout = options.Value.TimeoutMilliseconds;
                configuration.AsyncTimeout = options.Value.TimeoutMilliseconds;
                configuration.SyncTimeout = options.Value.TimeoutMilliseconds;
                configuration.BacklogPolicy = BacklogPolicy.FailFast;
                configuration.IncludeDetailInExceptions = false;
                _connection = ConnectionMultiplexer.ConnectAsync(configuration);
            }

            connection = _connection;
        }

        var multiplexer = await connection.WaitAsync(
            TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds + 250), cancellationToken);
        if (!multiplexer.IsConnected)
        {
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Cache is unavailable.");
        }

        return multiplexer.GetDatabase();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            try
            {
                var connection = await _connection;
                await connection.DisposeAsync();
            }
            catch (RedisException)
            {
                // A failed initial connection has no multiplexer to dispose.
            }
        }
    }
}
