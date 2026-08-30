namespace StoreControl.Domain.Sales;

public sealed class SalePayment
{
    private const decimal MaximumMoney = 999_999_999_999.99m;

    private SalePayment()
    {
    }

    internal SalePayment(Sale sale, PaymentMethod method, decimal amount, string? reference)
    {
        Sale = sale ?? throw new ArgumentNullException(nameof(sale));
        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), "A forma de pagamento é inválida.");
        }

        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (normalizedAmount <= 0 || normalizedAmount > MaximumMoney)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "O pagamento deve estar entre 0,01 e 999.999.999.999,99.");
        }

        Method = method;
        Amount = normalizedAmount;
        Reference = NormalizeOptional(reference, 100, nameof(reference));
    }

    public long Id { get; private set; }

    public long SaleId { get; private set; }

    public Sale Sale { get; private set; } = null!;

    public PaymentMethod Method { get; private set; }

    public decimal Amount { get; private set; }

    public string? Reference { get; private set; }

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
