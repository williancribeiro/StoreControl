using StoreControl.Domain.Catalog;
using StoreControl.Domain.Stores;

namespace StoreControl.Domain.Inventory;

public sealed class StockMovement
{
    private StockMovement()
    {
    }

    internal StockMovement(
        int storeId,
        int productId,
        StockMovementType type,
        decimal quantityChange,
        decimal quantityBefore,
        decimal quantityAfter,
        string reason,
        Guid createdByUserId,
        string correlationId,
        string? referenceType,
        string? referenceId)
    {
        StoreId = storeId;
        ProductId = productId;
        Type = type;
        QuantityChange = quantityChange;
        QuantityBefore = quantityBefore;
        QuantityAfter = quantityAfter;
        Reason = NormalizeRequired(reason, 500, nameof(reason));
        CreatedByUserId = createdByUserId != Guid.Empty
            ? createdByUserId
            : throw new ArgumentException("Informe o usuário responsável.", nameof(createdByUserId));
        CorrelationId = NormalizeRequired(correlationId, 128, nameof(correlationId));
        ReferenceType = NormalizeOptional(referenceType, 50, nameof(referenceType));
        ReferenceId = NormalizeOptional(referenceId, 100, nameof(referenceId));

        if (ReferenceId is not null && ReferenceType is null)
        {
            throw new ArgumentException("Informe o tipo da referência quando houver um identificador.", nameof(referenceType));
        }

        OccurredAtUtc = DateTime.UtcNow;
    }

    public long Id { get; private set; }

    public int StoreId { get; private set; }

    public Store Store { get; private set; } = null!;

    public int ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public StockMovementType Type { get; private set; }

    public decimal QuantityChange { get; private set; }

    public decimal QuantityBefore { get; private set; }

    public decimal QuantityAfter { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string? ReferenceType { get; private set; }

    public string? ReferenceId { get; private set; }

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
