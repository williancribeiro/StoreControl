using StoreControl.Domain.Stores;

namespace StoreControl.Domain.Inventory;

public sealed class StockTransfer
{
    private readonly List<StockTransferItem> _items = [];
    private readonly List<StockTransferStatusHistory> _statusHistory = [];

    private StockTransfer()
    {
    }

    public StockTransfer(
        Guid operationId,
        int originStoreId,
        int destinationStoreId,
        Guid createdByUserId,
        string? notes)
    {
        OperationId = ValidateOperationId(operationId, nameof(operationId));
        OriginStoreId = ValidateId(originStoreId, nameof(originStoreId));
        DestinationStoreId = ValidateId(destinationStoreId, nameof(destinationStoreId));
        if (OriginStoreId == DestinationStoreId)
        {
            throw new ArgumentException("As lojas de origem e destino devem ser diferentes.", nameof(destinationStoreId));
        }

        CreatedByUserId = ValidateUser(createdByUserId, nameof(createdByUserId));
        Notes = NormalizeOptional(notes, 1000, nameof(notes));
        Status = StockTransferStatus.Created;
        CreatedAtUtc = DateTime.UtcNow;
        _statusHistory.Add(new StockTransferStatusHistory(
            this,
            Status,
            CreatedByUserId,
            CreatedAtUtc,
            "Transferência criada."));
    }

    public long Id { get; private set; }

    public Guid OperationId { get; private set; }

    public int OriginStoreId { get; private set; }

    public Store OriginStore { get; private set; } = null!;

    public int DestinationStoreId { get; private set; }

    public Store DestinationStore { get; private set; } = null!;

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public StockTransferStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public Guid? DispatchOperationId { get; private set; }

    public Guid? DispatchedByUserId { get; private set; }

    public DateTime? DispatchedAtUtc { get; private set; }

    public Guid? ReceiptOperationId { get; private set; }

    public Guid? ReceivedByUserId { get; private set; }

    public DateTime? ReceivedAtUtc { get; private set; }

    public Guid? CancellationOperationId { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public string? CancellationReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<StockTransferItem> Items => _items.AsReadOnly();

    public IReadOnlyCollection<StockTransferStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public void AddItem(int productId, string productCode, string productName, decimal quantity)
    {
        EnsureStatus(StockTransferStatus.Created);
        if (_items.Any(item => item.ProductId == productId))
        {
            throw new InvalidOperationException($"O produto {productCode} já foi incluído na transferência.");
        }

        _items.Add(new StockTransferItem(this, productId, productCode, productName, quantity));
    }

    public void Dispatch(Guid operationId, Guid userId)
    {
        EnsureStatus(StockTransferStatus.Created);
        EnsureHasItems();
        var validOperationId = ValidateOperationId(operationId, nameof(operationId));
        var validUserId = ValidateUser(userId, nameof(userId));
        DispatchOperationId = validOperationId;
        DispatchedByUserId = validUserId;
        DispatchedAtUtc = DateTime.UtcNow;
        Status = StockTransferStatus.InTransit;
        _statusHistory.Add(new StockTransferStatusHistory(
            this,
            Status,
            DispatchedByUserId.Value,
            DispatchedAtUtc.Value,
            "Mercadoria expedida da loja de origem."));
    }

    public void Receive(Guid operationId, Guid userId)
    {
        EnsureStatus(StockTransferStatus.InTransit);
        var validOperationId = ValidateOperationId(operationId, nameof(operationId));
        var validUserId = ValidateUser(userId, nameof(userId));
        ReceiptOperationId = validOperationId;
        ReceivedByUserId = validUserId;
        ReceivedAtUtc = DateTime.UtcNow;
        Status = StockTransferStatus.Received;
        _statusHistory.Add(new StockTransferStatusHistory(
            this,
            Status,
            ReceivedByUserId.Value,
            ReceivedAtUtc.Value,
            "Mercadoria recebida na loja de destino."));
    }

    public void Cancel(Guid operationId, Guid userId, string reason)
    {
        EnsureStatus(StockTransferStatus.Created);
        var validOperationId = ValidateOperationId(operationId, nameof(operationId));
        var validUserId = ValidateUser(userId, nameof(userId));
        var validReason = NormalizeRequired(reason, 500, nameof(reason));
        CancellationOperationId = validOperationId;
        CancelledByUserId = validUserId;
        CancellationReason = validReason;
        CancelledAtUtc = DateTime.UtcNow;
        Status = StockTransferStatus.Cancelled;
        _statusHistory.Add(new StockTransferStatusHistory(
            this,
            Status,
            CancelledByUserId.Value,
            CancelledAtUtc.Value,
            CancellationReason));
    }

    private void EnsureHasItems()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Inclua pelo menos um item na transferência.");
        }
    }

    private void EnsureStatus(StockTransferStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"A transferência no status {Status} não permite esta operação.");
        }
    }

    private static int ValidateId(int value, string parameterName) => value > 0
        ? value
        : throw new ArgumentException("Informe um identificador válido.", parameterName);

    private static Guid ValidateOperationId(Guid value, string parameterName) => value != Guid.Empty
        ? value
        : throw new ArgumentException("Informe um identificador de operação válido.", parameterName);

    private static Guid ValidateUser(Guid value, string parameterName) => value != Guid.Empty
        ? value
        : throw new ArgumentException("Informe o usuário responsável.", parameterName);

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"O valor deve possuir entre 1 e {maximumLength} caracteres.",
                parameterName);
        }

        return normalized;
    }

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
