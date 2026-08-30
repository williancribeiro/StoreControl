namespace StoreControl.Domain.Sales;

public sealed class SaleStatusHistory
{
    private SaleStatusHistory()
    {
    }

    internal SaleStatusHistory(
        Sale sale,
        SaleStatus status,
        Guid changedByUserId,
        DateTime changedAtUtc,
        string description)
    {
        Sale = sale ?? throw new ArgumentNullException(nameof(sale));
        Status = status;
        ChangedByUserId = changedByUserId;
        ChangedAtUtc = changedAtUtc;
        Description = description;
    }

    public long Id { get; private set; }

    public long SaleId { get; private set; }

    public Sale Sale { get; private set; } = null!;

    public SaleStatus Status { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public string Description { get; private set; } = string.Empty;
}
