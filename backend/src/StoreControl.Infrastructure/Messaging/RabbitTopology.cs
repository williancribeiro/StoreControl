using RabbitMQ.Client;

namespace StoreControl.Infrastructure.Messaging;

internal sealed class RabbitTopology(MessagingOptions options)
{
    public string EventsExchange { get; } = $"{options.TopologyPrefix}.events";
    public string RetryExchange { get; } = $"{options.TopologyPrefix}.retry";
    public string DeadLetterExchange { get; } = $"{options.TopologyPrefix}.dead-letter";
    public string SaleFinalizedQueue { get; } = $"{options.TopologyPrefix}.worker.sale-finalized";
    public string SaleFinalizedDeadLetterQueue { get; } = $"{options.TopologyPrefix}.worker.sale-finalized.dlq";
    public string SaleFinalizedDeadLetterRoutingKey { get; } = $"{MessagingContracts.SaleFinalizedRoutingKey}.dead";

    public string RetryRoutingKey(int zeroBasedAttempt) =>
        $"{MessagingContracts.SaleFinalizedRoutingKey}.retry.{zeroBasedAttempt + 1}";

    public string RetryQueue(int zeroBasedAttempt) =>
        $"{SaleFinalizedQueue}.retry.{zeroBasedAttempt + 1}";

    public async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false,
            arguments: null, passive: false, noWait: false, cancellationToken);
        await channel.ExchangeDeclareAsync(
            RetryExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            arguments: null, passive: false, noWait: false, cancellationToken);
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            arguments: null, passive: false, noWait: false, cancellationToken);

        var mainArguments = new Dictionary<string, object?>
        {
            [Headers.XDeadLetterExchange] = DeadLetterExchange,
            [Headers.XDeadLetterRoutingKey] = SaleFinalizedDeadLetterRoutingKey
        };
        await channel.QueueDeclareAsync(
            SaleFinalizedQueue, durable: true, exclusive: false, autoDelete: false,
            mainArguments, passive: false, noWait: false, cancellationToken);
        await channel.QueueBindAsync(
            SaleFinalizedQueue, EventsExchange, MessagingContracts.SaleFinalizedRoutingKey,
            arguments: null, noWait: false, cancellationToken);

        for (var index = 0; index < options.RetryDelaysMilliseconds.Length; index++)
        {
            var retryArguments = new Dictionary<string, object?>
            {
                [Headers.XMessageTTL] = options.RetryDelaysMilliseconds[index],
                [Headers.XDeadLetterExchange] = EventsExchange,
                [Headers.XDeadLetterRoutingKey] = MessagingContracts.SaleFinalizedRoutingKey
            };
            var retryQueue = RetryQueue(index);
            await channel.QueueDeclareAsync(
                retryQueue, durable: true, exclusive: false, autoDelete: false,
                retryArguments, passive: false, noWait: false, cancellationToken);
            await channel.QueueBindAsync(
                retryQueue, RetryExchange, RetryRoutingKey(index),
                arguments: null, noWait: false, cancellationToken);
        }

        await channel.QueueDeclareAsync(
            SaleFinalizedDeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: null, passive: false, noWait: false, cancellationToken);
        await channel.QueueBindAsync(
            SaleFinalizedDeadLetterQueue, DeadLetterExchange, SaleFinalizedDeadLetterRoutingKey,
            arguments: null, noWait: false, cancellationToken);
    }
}
