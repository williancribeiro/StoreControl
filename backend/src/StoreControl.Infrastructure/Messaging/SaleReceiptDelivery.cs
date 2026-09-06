namespace StoreControl.Infrastructure.Messaging;

internal enum SaleReceiptDeliveryStatus
{
    SimulatedSent = 1,
    SkippedNoDestination = 2
}

internal sealed class SaleReceiptDelivery
{
    private SaleReceiptDelivery()
    {
    }

    public SaleReceiptDelivery(
        Guid messageId,
        long saleId,
        SaleReceiptDeliveryStatus status,
        byte[]? recipientHash,
        DateTime processedAtUtc)
    {
        MessageId = messageId;
        SaleId = saleId;
        Status = status;
        RecipientHash = recipientHash;
        ProcessedAtUtc = processedAtUtc;
    }

    public long Id { get; private set; }
    public Guid MessageId { get; private set; }
    public long SaleId { get; private set; }
    public SaleReceiptDeliveryStatus Status { get; private set; }
    public byte[]? RecipientHash { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }
}
