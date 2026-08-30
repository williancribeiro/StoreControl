namespace StoreControl.Domain.Inventory;

public sealed class StockTransferItem
{
    private const decimal MaximumQuantity = 999_999_999_999.999m;

    private StockTransferItem()
    {
    }

    internal StockTransferItem(
        StockTransfer transfer,
        int productId,
        string productCode,
        string productName,
        decimal quantity)
    {
        Transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
        ProductId = productId > 0
            ? productId
            : throw new ArgumentException("Informe um produto válido.", nameof(productId));
        ProductCode = NormalizeRequired(productCode, 50, nameof(productCode));
        ProductName = NormalizeRequired(productName, 200, nameof(productName));
        Quantity = NormalizeQuantity(quantity);
    }

    public long Id { get; private set; }

    public long StockTransferId { get; private set; }

    public StockTransfer Transfer { get; private set; } = null!;

    public int ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

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
