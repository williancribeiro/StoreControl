namespace StoreControl.Infrastructure.Messaging;

internal sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public InboxMessage(string consumer, Guid messageId, byte[] payloadHash, DateTime processedAtUtc)
    {
        Consumer = consumer;
        MessageId = messageId;
        PayloadHash = payloadHash;
        ProcessedAtUtc = processedAtUtc;
    }

    public string Consumer { get; private set; } = string.Empty;
    public Guid MessageId { get; private set; }
    public byte[] PayloadHash { get; private set; } = [];
    public DateTime ProcessedAtUtc { get; private set; }
}
