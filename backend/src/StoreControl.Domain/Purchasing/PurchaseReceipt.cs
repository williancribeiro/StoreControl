namespace StoreControl.Domain.Purchasing;

public sealed class PurchaseReceipt
{
    private readonly List<PurchaseReceiptItem> _items = [];

    private PurchaseReceipt()
    {
    }

    internal PurchaseReceipt(
        PurchaseOrder purchaseOrder,
        Guid id,
        Guid receivedByUserId,
        string? notes)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Informe um identificador de recebimento válido.", nameof(id));
        }

        if (receivedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Informe o usuário responsável.", nameof(receivedByUserId));
        }

        Id = id;
        PurchaseOrder = purchaseOrder;
        ReceivedByUserId = receivedByUserId;
        Notes = NormalizeOptional(notes, 500, nameof(notes));
        ReceivedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public long PurchaseOrderId { get; private set; }

    public PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public Guid ReceivedByUserId { get; private set; }

    public DateTime ReceivedAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public IReadOnlyCollection<PurchaseReceiptItem> Items => _items.AsReadOnly();

    internal void AddItem(PurchaseOrderItem purchaseOrderItem, decimal quantity) =>
        _items.Add(new PurchaseReceiptItem(this, purchaseOrderItem, quantity));

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new ArgumentException($"O valor deve possuir no máximo {maximumLength} caracteres.", parameterName);
        }

        return normalized;
    }
}
