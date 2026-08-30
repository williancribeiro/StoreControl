namespace StoreControl.Domain.Purchasing;

public sealed class PurchaseStatusHistory
{
    private PurchaseStatusHistory()
    {
    }

    internal PurchaseStatusHistory(
        PurchaseOrder purchaseOrder,
        PurchaseOrderStatus status,
        Guid changedByUserId,
        string description)
    {
        PurchaseOrder = purchaseOrder;
        Status = status;
        ChangedByUserId = changedByUserId != Guid.Empty
            ? changedByUserId
            : throw new ArgumentException("Informe o usuário responsável.", nameof(changedByUserId));
        Description = NormalizeDescription(description);
        ChangedAtUtc = DateTime.UtcNow;
    }

    public long Id { get; private set; }

    public long PurchaseOrderId { get; private set; }

    public PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public PurchaseOrderStatus Status { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public DateTime ChangedAtUtc { get; private set; }

    public string Description { get; private set; } = string.Empty;

    private static string NormalizeDescription(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 300)
        {
            throw new ArgumentException("A descrição deve possuir entre 1 e 300 caracteres.", nameof(value));
        }

        return normalized;
    }
}
