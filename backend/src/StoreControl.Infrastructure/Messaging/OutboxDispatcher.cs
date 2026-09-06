using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StoreControl.Infrastructure.Messaging;

internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    RabbitMqPublisher publisher,
    IOptions<MessagingOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly MessagingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("RabbitMQ messaging is disabled; the Outbox dispatcher is inactive");
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }

        logger.LogInformation(
            "Outbox dispatcher started with batch size {BatchSize}",
            _options.OutboxBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var publishedAny = await DispatchBatchAsync(stoppingToken);
                if (!publishedAny)
                {
                    await Task.Delay(_options.OutboxPollingMilliseconds, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Outbox dispatch cycle failed; pending messages remain durable in SQL Server");
                await Task.Delay(_options.OutboxPollingMilliseconds, stoppingToken);
            }
        }

        logger.LogInformation("Outbox dispatcher stopped");
    }

    private async Task<bool> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<OutboxStore>();
        var messages = await store.ClaimAsync(
            _options.OutboxBatchSize,
            TimeSpan.FromSeconds(_options.OutboxLeaseSeconds),
            cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishOutboxAsync(message, cancellationToken);
                await store.MarkPublishedAsync(message, cancellationToken);
                logger.LogInformation(
                    "Integration event {MessageId} ({EventType}) published after {PublishAttempts} attempt(s)",
                    message.Id,
                    message.Type,
                    message.PublishAttempts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await store.MarkFailedAsync(message, exception, cancellationToken);
                logger.LogWarning(
                    exception,
                    "Failed to publish integration event {MessageId}; SQL retry was scheduled",
                    message.Id);
            }
        }

        return messages.Count > 0;
    }
}
