using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;

namespace StoreControl.Infrastructure.Messaging;

internal sealed class RabbitMqPublisher(
    RabbitMqConnection connection,
    RabbitTopology topology) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishOutboxAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        using var activity = MessagingTelemetry.Activities.StartActivity(
            $"{topology.EventsExchange} publish",
            ActivityKind.Producer,
            RabbitMessageHeaders.ParentContext(message.TraceParent, message.TraceState));
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", topology.EventsExchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", message.RoutingKey);
        activity?.SetTag("messaging.message.id", message.Id);

        try
        {
            var properties = CreateProperties(
                message.Id, message.Type, message.CorrelationId, message.OccurredAtUtc, retryCount: 0, activity);
            await PublishAsync(
                topology.EventsExchange, message.RoutingKey, properties,
                Encoding.UTF8.GetBytes(message.Payload), cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    public async Task PublishAsync(
        string exchange,
        string routingKey,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.BasicPublishAsync(
                exchange, routingKey, mandatory: true, properties, body, cancellationToken);
            MessagingTelemetry.RecordPublication("confirmed", started);
        }
        catch
        {
            MessagingTelemetry.RecordPublication("failed", started);
            await ResetChannelAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static BasicProperties CreateProperties(
        Guid messageId,
        string type,
        string correlationId,
        DateTime occurredAtUtc,
        int retryCount,
        Activity? activity)
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-retry-count"] = retryCount
        };

        if (activity?.Id is not null)
        {
            headers["traceparent"] = Encoding.UTF8.GetBytes(activity.Id);
        }

        if (!string.IsNullOrWhiteSpace(activity?.TraceStateString))
        {
            headers["tracestate"] = Encoding.UTF8.GetBytes(activity.TraceStateString);
        }

        return new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = messageId.ToString("D"),
            Type = type,
            CorrelationId = correlationId,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(occurredAtUtc).ToUnixTimeSeconds()),
            Headers = headers
        };
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel?.IsOpen == true)
        {
            return _channel;
        }

        await ResetChannelAsync();
        var rabbitConnection = await connection.GetAsync(cancellationToken);
        _channel = await rabbitConnection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await topology.DeclareAsync(_channel, cancellationToken);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await ResetChannelAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
