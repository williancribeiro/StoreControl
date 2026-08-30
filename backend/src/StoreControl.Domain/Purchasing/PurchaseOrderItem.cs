using StoreControl.Domain.Catalog;

namespace StoreControl.Domain.Purchasing;

public sealed class PurchaseOrderItem
{
    private const decimal MaximumQuantity = 999_999_999_999.999m;
    private const decimal MaximumMoney = 999_999_999_999.99m;

    private PurchaseOrderItem()
    {
    }

    internal PurchaseOrderItem(
        PurchaseOrder purchaseOrder,
        int productId,
        string productCode,
        string productName,
        decimal orderedQuantity,
        decimal unitCost)
    {
        PurchaseOrder = purchaseOrder;
        ProductId = productId > 0
            ? productId
            : throw new ArgumentException("Informe um produto válido.", nameof(productId));
        ProductCode = NormalizeRequired(productCode, 30, nameof(productCode));
        ProductName = NormalizeRequired(productName, 160, nameof(productName));
        OrderedQuantity = NormalizeQuantity(orderedQuantity, nameof(orderedQuantity));
        UnitCost = NormalizeMoney(unitCost, 4, nameof(unitCost));
        LineTotal = CalculateLineTotal(OrderedQuantity, UnitCost);
    }

    public long Id { get; private set; }

    public long PurchaseOrderId { get; private set; }

    public PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public int ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public decimal OrderedQuantity { get; private set; }

    public decimal ReceivedQuantity { get; private set; }

    public decimal UnitCost { get; private set; }

    public decimal LineTotal { get; private set; }

    public decimal RemainingQuantity => OrderedQuantity - ReceivedQuantity;

    internal void Receive(decimal quantity)
    {
        var normalized = NormalizeQuantity(quantity, nameof(quantity));
        if (normalized > RemainingQuantity)
        {
            throw new InvalidOperationException(
                $"A quantidade recebida do produto {ProductCode} excede o saldo pendente de {RemainingQuantity:0.###}.");
        }

        ReceivedQuantity += normalized;
    }

    private static decimal NormalizeQuantity(decimal value, string parameterName)
    {
        var normalized = decimal.Round(value, 3, MidpointRounding.AwayFromZero);
        if (normalized <= 0 || normalized > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A quantidade deve estar entre 0,001 e 999.999.999.999,999.");
        }

        return normalized;
    }

    private static decimal NormalizeMoney(decimal value, int scale, string parameterName)
    {
        var normalized = decimal.Round(value, scale, MidpointRounding.AwayFromZero);
        if (normalized < 0 || normalized > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "O custo unitário deve estar entre zero e 999.999.999.999,99.");
        }

        return normalized;
    }

    private static decimal CalculateLineTotal(decimal quantity, decimal unitCost)
    {
        var total = decimal.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);
        if (total > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(nameof(unitCost), "O total do item excede o limite permitido.");
        }

        return total;
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
}
