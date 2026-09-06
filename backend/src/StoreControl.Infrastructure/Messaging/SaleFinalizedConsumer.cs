using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace StoreControl.Infrastructure.Messaging;

internal sealed class SaleFinalizedConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnection connection,
    RabbitMqPublisher publisher,
    RabbitTopology topology,
    IOptions<MessagingOptions> options,
    ILogger<SaleFinalizedConsumer> logger) : BackgroundService
{
    private readonly MessagingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("RabbitMQ messaging is disabled; the sale event consumer is inactive");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilDisconnectedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sale event consumer disconnected; recovery will be attempted");
                await Task.Delay(TimeSpan.FromSeconds(_options.RecoveryIntervalSeconds), stoppingToken);
            }
        }

        logger.LogInformation("Sale event consumer stopped");
    }

    private async Task ConsumeUntilDisconnectedAsync(CancellationToken cancellationToken)
    {
        var rabbitConnection = await connection.GetAsync(cancellationToken);
        await using var channel = await rabbitConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        await topology.DeclareAsync(channel, cancellationToken);
        await channel.BasicQosAsync(0, _options.ConsumerPrefetch, global: false, cancellationToken);

        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, eventArgs) => HandleDeliveryAsync(channel, eventArgs, cancellationToken);
        var consumerTag = await channel.BasicConsumeAsync(
            topology.SaleFinalizedQueue,
            autoAck: false,
            consumer,
            cancellationToken);

        logger.LogInformation(
            "Sale event consumer attached to {Queue} with prefetch {Prefetch}",
            topology.SaleFinalizedQueue,
            _options.ConsumerPrefetch);

        await Task.WhenAny(Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken), disconnected.Task);
        if (channel.IsOpen)
        {
            await channel.BasicCancelAsync(consumerTag, noWait: false, CancellationToken.None);
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        Guid messageId = default;
        int retryCount = 0;
        var started = Stopwatch.GetTimestamp();
        Activity? activity = null;
        IDisposable? logScope = null;
        try
        {
            messageId = ParseEnvelope(delivery.BasicProperties);
            retryCount = RabbitMessageHeaders.RetryCount(delivery.BasicProperties);
            activity = MessagingTelemetry.Activities.StartActivity(
                $"{topology.SaleFinalizedQueue} process",
                ActivityKind.Consumer,
                RabbitMessageHeaders.ParentContext(delivery.BasicProperties));
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", topology.SaleFinalizedQueue);
            activity?.SetTag("messaging.message.id", messageId);
            activity?.SetTag("messaging.message.retry_count", retryCount);
            logScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = delivery.BasicProperties.CorrelationId ?? messageId.ToString("D"),
                ["MessageId"] = messageId,
                ["TraceId"] = activity?.TraceId.ToString(),
                ["SpanId"] = activity?.SpanId.ToString()
            });

            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<SaleFinalizedProcessor>();
            var outcome = await processor.ProcessAsync(messageId, delivery.Body, stoppingToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            var telemetryOutcome = outcome == SaleFinalizedProcessingOutcome.Duplicate ? "duplicate" : "processed";
            MessagingTelemetry.RecordDelivery(telemetryOutcome, started);
            activity?.SetStatus(ActivityStatusCode.Ok);
            logger.LogInformation(
                "Sale event {MessageId} processed with outcome {Outcome} on attempt {Attempt}",
                messageId,
                outcome,
                retryCount + 1);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Channel disposal requeues the unacknowledged delivery.
        }
        catch (PermanentMessageException exception)
        {
            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error);
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, stoppingToken);
            MessagingTelemetry.RecordDelivery("dead_lettered", started);
            logger.LogWarning(
                exception,
                "Sale event {MessageId} was rejected as permanent and routed to the DLQ",
                messageId);
        }
        catch (Exception exception)
        {
            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error);
            await HandleTransientFailureAsync(
                channel, delivery, messageId, retryCount, exception, started, stoppingToken);
        }
        finally
        {
            logScope?.Dispose();
            activity?.Dispose();
        }
    }

    private async Task HandleTransientFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        Guid messageId,
        int retryCount,
        Exception exception,
        long started,
        CancellationToken cancellationToken)
    {
        if (retryCount >= _options.RetryDelaysMilliseconds.Length)
        {
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
            MessagingTelemetry.RecordDelivery("retries_exhausted", started);
            logger.LogError(
                exception,
                "Sale event {MessageId} exhausted {RetryCount} retries and was routed to the DLQ",
                messageId,
                retryCount);
            return;
        }

        Activity? publishActivity = null;
        try
        {
            publishActivity = MessagingTelemetry.Activities.StartActivity(
                $"{topology.RetryExchange} publish",
                ActivityKind.Producer);
            var properties = RabbitMqPublisher.CreateProperties(
                messageId,
                delivery.BasicProperties.Type ?? MessagingContracts.SaleFinalizedType,
                delivery.BasicProperties.CorrelationId ?? messageId.ToString("D"),
                DateTime.UtcNow,
                retryCount + 1,
                publishActivity);
            await publisher.PublishAsync(
                topology.RetryExchange,
                topology.RetryRoutingKey(retryCount),
                properties,
                delivery.Body,
                cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            publishActivity?.SetStatus(ActivityStatusCode.Ok);
            MessagingTelemetry.RecordDelivery("retry_scheduled", started);
            logger.LogWarning(
                exception,
                "Sale event {MessageId} scheduled for retry {RetryCount} after {DelayMilliseconds} ms",
                messageId,
                retryCount + 1,
                _options.RetryDelaysMilliseconds[retryCount]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Channel disposal requeues the original delivery.
        }
        catch (Exception retryException)
        {
            publishActivity?.SetTag("error.type", retryException.GetType().FullName);
            publishActivity?.SetStatus(ActivityStatusCode.Error);
            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken);
            logger.LogError(
                retryException,
                "Retry publication failed for sale event {MessageId}; original delivery was requeued",
                messageId);
        }
        finally
        {
            publishActivity?.Dispose();
        }
    }

    private static Guid ParseEnvelope(IReadOnlyBasicProperties properties)
    {
        if (!string.Equals(properties.Type, MessagingContracts.SaleFinalizedType, StringComparison.Ordinal)
            || !Guid.TryParseExact(properties.MessageId, "D", out var messageId)
            || messageId == Guid.Empty)
        {
            throw new PermanentMessageException("The message envelope is missing a supported type or message ID.");
        }

        return messageId;
    }
}
