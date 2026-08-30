using StoreControl.Domain.Stores;

namespace StoreControl.Domain.Cash;

public sealed class CashRegister
{
    private CashRegister()
    {
    }

    public CashRegister(int storeId, string code, string name)
    {
        StoreId = storeId > 0
            ? storeId
            : throw new ArgumentException("Informe uma loja válida.", nameof(storeId));
        Code = NormalizeRequired(code, 30, nameof(code)).ToUpperInvariant();
        Name = NormalizeRequired(name, 100, nameof(name));
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int Id { get; private set; }

    public int StoreId { get; private set; }

    public Store Store { get; private set; } = null!;

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

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
