namespace StoreControl.Domain.Catalog;

public sealed class Category
{
    private Category()
    {
    }

    public Category(string code, string name, string? description)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Update(string code, string name, string? description, bool isActive)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 30)
        {
            throw new ArgumentException("O código da categoria deve possuir entre 1 e 30 caracteres.", nameof(code));
        }

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 120)
        {
            throw new ArgumentException("O nome da categoria deve possuir entre 1 e 120 caracteres.", nameof(name));
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalized?.Length > 500)
        {
            throw new ArgumentException("A descrição da categoria deve possuir no máximo 500 caracteres.", nameof(description));
        }

        return normalized;
    }
}
