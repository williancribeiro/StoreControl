using StoreControl.Domain.Catalog;
using StoreControl.Domain.Stores;

namespace StoreControl.Domain.Inventory;

public sealed class StockBalance
{
    private const decimal MaximumQuantity = 999_999_999_999.999m;

    private StockBalance()
    {
    }

    public StockBalance(int storeId, int productId, decimal minimumQuantity = 0)
    {
        StoreId = ValidateId(storeId, nameof(storeId));
        ProductId = ValidateId(productId, nameof(productId));
        MinimumQuantity = NormalizeQuantity(minimumQuantity, false, nameof(minimumQuantity));
        QuantityOnHand = 0;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int StoreId { get; private set; }

    public Store Store { get; private set; } = null!;

    public int ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public decimal QuantityOnHand { get; private set; }

    public decimal MinimumQuantity { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsBelowMinimum => MinimumQuantity > 0 && QuantityOnHand <= MinimumQuantity;

    public void UpdateMinimumQuantity(decimal minimumQuantity)
    {
        MinimumQuantity = NormalizeQuantity(minimumQuantity, false, nameof(minimumQuantity));
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public StockMovement ApplyMovement(
        StockMovementType type,
        decimal quantity,
        string reason,
        Guid createdByUserId,
        string correlationId,
        string? referenceType = null,
        string? referenceId = null)
    {
        var normalizedQuantity = NormalizeQuantity(quantity, true, nameof(quantity));
        var direction = GetDirection(type);
        var change = direction * normalizedQuantity;
        var quantityBefore = QuantityOnHand;
        var quantityAfter = quantityBefore + change;

        if (quantityAfter < 0)
        {
            throw new InvalidOperationException(
                $"Estoque insuficiente. Disponível: {quantityBefore:0.###}; solicitado: {normalizedQuantity:0.###}.");
        }

        if (quantityAfter > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A quantidade resultante excede o limite permitido.");
        }

        var movement = new StockMovement(
            StoreId,
            ProductId,
            type,
            change,
            quantityBefore,
            quantityAfter,
            reason,
            createdByUserId,
            correlationId,
            referenceType,
            referenceId);

        QuantityOnHand = quantityAfter;
        UpdatedAtUtc = movement.OccurredAtUtc;
        return movement;
    }

    private static int ValidateId(int id, string parameterName)
    {
        if (id <= 0)
        {
            throw new ArgumentException("Informe um identificador válido.", parameterName);
        }

        return id;
    }

    private static decimal NormalizeQuantity(decimal quantity, bool mustBePositive, string parameterName)
    {
        var normalized = decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);
        if (normalized < 0 || normalized > MaximumQuantity || (mustBePositive && normalized == 0))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                mustBePositive
                    ? "A quantidade deve estar entre 0,001 e 999.999.999.999,999."
                    : "A quantidade deve estar entre zero e 999.999.999.999,999.");
        }

        return normalized;
    }

    private static int GetDirection(StockMovementType type) => type switch
    {
        StockMovementType.PurchaseReceipt or
        StockMovementType.PositiveAdjustment or
        StockMovementType.TransferIn or
        StockMovementType.SaleReversal or
        StockMovementType.OtherIn or
        StockMovementType.InitialBalance => 1,
        StockMovementType.Sale or
        StockMovementType.NegativeAdjustment or
        StockMovementType.TransferOut or
        StockMovementType.OtherOut => -1,
        _ => throw new ArgumentOutOfRangeException(nameof(type), "O tipo de movimentação é inválido.")
    };
}
