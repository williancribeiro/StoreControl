namespace StoreControl.Domain.Inventory;

public sealed class StockTransferStatusHistory
{
    private StockTransferStatusHistory()
    {
    }

    internal StockTransferStatusHistory(
        StockTransfer transfer,
        StockTransferStatus status,
        Guid changedByUserId,
        DateTime changedAtUtc,
        string description)
    {
        Transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
        Status = status;
        ChangedByUserId = changedByUserId;
        ChangedAtUtc = changedAtUtc;
        Description = description;
    }

    public long Id { get; private set; }

    public long StockTransferId { get; private set; }

    public StockTransfer Transfer { get; private set; } = null!;

    public StockTransferStatus Status { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public string Description { get; private set; } = string.Empty;
}
