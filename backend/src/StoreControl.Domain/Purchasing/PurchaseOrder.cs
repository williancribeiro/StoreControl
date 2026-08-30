using StoreControl.Domain.Parties;
using StoreControl.Domain.Stores;

namespace StoreControl.Domain.Purchasing;

public sealed class PurchaseOrder
{
    private const decimal MaximumMoney = 999_999_999_999.99m;
    private readonly List<PurchaseOrderItem> _items = [];
    private readonly List<PurchaseReceipt> _receipts = [];
    private readonly List<PurchaseStatusHistory> _statusHistory = [];

    private PurchaseOrder()
    {
    }

    public PurchaseOrder(
        int storeId,
        int supplierId,
        Guid createdByUserId,
        DateOnly? expectedDate,
        string? notes)
    {
        StoreId = storeId > 0
            ? storeId
            : throw new ArgumentException("Informe uma loja válida.", nameof(storeId));
        SupplierId = supplierId > 0
            ? supplierId
            : throw new ArgumentException("Informe um fornecedor válido.", nameof(supplierId));
        CreatedByUserId = createdByUserId != Guid.Empty
            ? createdByUserId
            : throw new ArgumentException("Informe o usuário responsável.", nameof(createdByUserId));
        ExpectedDate = expectedDate;
        Notes = NormalizeOptional(notes, 1000, nameof(notes));
        Status = PurchaseOrderStatus.Created;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        _statusHistory.Add(new PurchaseStatusHistory(this, Status, createdByUserId, "Pedido criado."));
    }

    public long Id { get; private set; }

    public int StoreId { get; private set; }

    public Store Store { get; private set; } = null!;

    public int SupplierId { get; private set; }

    public Supplier Supplier { get; private set; } = null!;

    public Guid CreatedByUserId { get; private set; }

    public PurchaseOrderStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateOnly? ExpectedDate { get; private set; }

    public string? Notes { get; private set; }

    public decimal TotalAmount { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();

    public IReadOnlyCollection<PurchaseReceipt> Receipts => _receipts.AsReadOnly();

    public IReadOnlyCollection<PurchaseStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public void AddItem(
        int productId,
        string productCode,
        string productName,
        decimal orderedQuantity,
        decimal unitCost)
    {
        EnsureStatus(PurchaseOrderStatus.Created, "adicionar itens");
        if (_items.Any(item => item.ProductId == productId))
        {
            throw new InvalidOperationException($"O produto {productCode} já foi incluído no pedido.");
        }

        var item = new PurchaseOrderItem(
            this,
            productId,
            productCode,
            productName,
            orderedQuantity,
            unitCost);
        var newTotal = decimal.Round(TotalAmount + item.LineTotal, 2, MidpointRounding.AwayFromZero);
        if (newTotal > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), "O total do pedido excede o limite permitido.");
        }

        _items.Add(item);
        TotalAmount = newTotal;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Submit(Guid changedByUserId)
    {
        EnsureStatus(PurchaseOrderStatus.Created, "enviar o pedido");
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Inclua pelo menos um item antes de enviar o pedido.");
        }

        TransitionTo(PurchaseOrderStatus.Pending, changedByUserId, "Pedido enviado para recebimento.");
    }

    public PurchaseReceipt Receive(
        Guid receiptId,
        IReadOnlyCollection<(long ItemId, decimal Quantity)> lines,
        Guid receivedByUserId,
        string? notes)
    {
        if (Status is not (PurchaseOrderStatus.Pending or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new InvalidOperationException(
                $"Não é possível receber um pedido com status {Status}.");
        }

        if (_receipts.Any(receipt => receipt.Id == receiptId))
        {
            throw new InvalidOperationException("Este recebimento já foi processado.");
        }

        if (lines.Count == 0)
        {
            throw new ArgumentException("Informe pelo menos um item para recebimento.", nameof(lines));
        }

        if (lines.Select(line => line.ItemId).Distinct().Count() != lines.Count)
        {
            throw new ArgumentException("Um item não pode aparecer mais de uma vez no recebimento.", nameof(lines));
        }

        var normalizedLines = lines.Select(line =>
        {
            var item = _items.SingleOrDefault(candidate => candidate.Id == line.ItemId)
                ?? throw new ArgumentException("O item informado não pertence ao pedido.", nameof(lines));
            var quantity = decimal.Round(line.Quantity, 3, MidpointRounding.AwayFromZero);
            if (quantity <= 0 || quantity > item.RemainingQuantity)
            {
                throw new InvalidOperationException(
                    $"A quantidade recebida do produto {item.ProductCode} deve estar entre 0,001 e {item.RemainingQuantity:0.###}.");
            }

            return (Item: item, Quantity: quantity);
        }).ToArray();

        var receipt = new PurchaseReceipt(this, receiptId, receivedByUserId, notes);
        foreach (var line in normalizedLines)
        {
            line.Item.Receive(line.Quantity);
            receipt.AddItem(line.Item, line.Quantity);
        }

        _receipts.Add(receipt);
        Status = _items.All(item => item.RemainingQuantity == 0)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;
        UpdatedAtUtc = receipt.ReceivedAtUtc;
        _statusHistory.Add(new PurchaseStatusHistory(
            this,
            Status,
            receivedByUserId,
            $"Recebimento {receipt.Id:D} registrado."));
        return receipt;
    }

    public void Finalize(Guid changedByUserId)
    {
        EnsureStatus(PurchaseOrderStatus.Received, "finalizar o pedido");
        TransitionTo(PurchaseOrderStatus.Finalized, changedByUserId, "Pedido finalizado.");
    }

    public void Cancel(Guid changedByUserId, string reason)
    {
        if (Status is not (PurchaseOrderStatus.Created or PurchaseOrderStatus.Pending))
        {
            throw new InvalidOperationException($"Não é possível cancelar um pedido com status {Status}.");
        }

        if (_receipts.Count > 0 || _items.Any(item => item.ReceivedQuantity > 0))
        {
            throw new InvalidOperationException("Não é possível cancelar um pedido que possui recebimentos.");
        }

        var normalizedReason = NormalizeRequired(reason, 250, nameof(reason));
        TransitionTo(PurchaseOrderStatus.Cancelled, changedByUserId, $"Pedido cancelado: {normalizedReason}");
    }

    private void TransitionTo(PurchaseOrderStatus status, Guid userId, string description)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Informe o usuário responsável.", nameof(userId));
        }

        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
        _statusHistory.Add(new PurchaseStatusHistory(this, status, userId, description));
    }

    private void EnsureStatus(PurchaseOrderStatus expected, string operation)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Não é possível {operation} quando o pedido está com status {Status}.");
        }
    }

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
