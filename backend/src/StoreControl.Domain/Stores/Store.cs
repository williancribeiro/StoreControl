namespace StoreControl.Domain.Stores;

public sealed class Store
{
    private Store()
    {
    }

    public Store(string code, string name)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Update(string code, string name, bool isActive)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20)
        {
            throw new ArgumentException("O código da loja deve possuir entre 1 e 20 caracteres.", nameof(code));
        }

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 120)
        {
            throw new ArgumentException("O nome da loja deve possuir entre 1 e 120 caracteres.", nameof(name));
        }

        return normalized;
    }
}
