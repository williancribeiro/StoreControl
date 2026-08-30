using StoreControl.Domain.Catalog;

namespace StoreControl.Domain.Sales;

public sealed class SaleItem
{
    private const decimal MaximumQuantity = 999_999_999_999.999m;
    private const decimal MaximumMoney = 999_999_999_999.99m;

    private SaleItem()
    {
    }

    internal SaleItem(
        Sale sale,
        int productId,
        string productCode,
        string productName,
        decimal quantity,
        decimal unitPrice)
    {
        Sale = sale ?? throw new ArgumentNullException(nameof(sale));
        ProductId = productId > 0
            ? productId
            : throw new ArgumentException("Informe um produto válido.", nameof(productId));
        ProductCode = NormalizeRequired(productCode, 30, nameof(productCode));
        ProductName = NormalizeRequired(productName, 160, nameof(productName));
        Quantity = NormalizeQuantity(quantity);
        UnitPrice = NormalizeMoney(unitPrice, nameof(unitPrice));
        LineSubtotal = decimal.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
        if (LineSubtotal > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "O subtotal do item excede o limite permitido.");
        }
    }

    public long Id { get; private set; }

    public long SaleId { get; private set; }

    public Sale Sale { get; private set; } = null!;

    public int ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineSubtotal { get; private set; }

    private static decimal NormalizeQuantity(decimal value)
    {
        var normalized = decimal.Round(value, 3, MidpointRounding.AwayFromZero);
        if (normalized <= 0 || normalized > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "A quantidade deve estar entre 0,001 e 999.999.999.999,999.");
        }

        return normalized;
    }

    private static decimal NormalizeMoney(decimal value, string parameterName)
    {
        var normalized = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (normalized < 0 || normalized > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "O preço deve estar entre zero e 999.999.999.999,99.");
        }

        return normalized;
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
