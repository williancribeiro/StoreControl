using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace StoreControl.Infrastructure.Messaging;

internal sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly MessagingOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<MessagingOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true)
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true)
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(_options.RecoveryIntervalSeconds),
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(_options.ConnectionTimeoutMilliseconds),
                ClientProvidedName = "storecontrol"
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
