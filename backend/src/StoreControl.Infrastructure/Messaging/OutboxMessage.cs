namespace StoreControl.Infrastructure.Messaging;

internal sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid id,
        string type,
        string routingKey,
        string payload,
        string correlationId,
        DateTime occurredAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Message ID is required.", nameof(id));
        Id = id;
        Type = type;
        RoutingKey = routingKey;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAtUtc = occurredAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
        NextAttemptAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public int PublishAttempts { get; private set; }
    public Guid? LockId { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public string? LastError { get; private set; }
}
