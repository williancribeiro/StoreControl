namespace StoreControl.Domain.Catalog;

public sealed class Product
{
    private Product()
    {
    }

    public Product(
        string code,
        string? barcode,
        string name,
        string? description,
        int categoryId,
        decimal costPrice,
        decimal salePrice)
    {
        Code = NormalizeCode(code);
        Barcode = NormalizeBarcode(barcode);
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        CategoryId = ValidateCategory(categoryId);
        CostPrice = ValidateMoney(costPrice, 4, nameof(costPrice));
        SalePrice = ValidateMoney(salePrice, 2, nameof(salePrice));
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string? Barcode { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public decimal CostPrice { get; private set; }

    public decimal SalePrice { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Update(
        string code,
        string? barcode,
        string name,
        string? description,
        int categoryId,
        decimal costPrice,
        decimal salePrice,
        bool isActive)
    {
        Code = NormalizeCode(code);
        Barcode = NormalizeBarcode(barcode);
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        CategoryId = ValidateCategory(categoryId);
        CostPrice = ValidateMoney(costPrice, 4, nameof(costPrice));
        SalePrice = ValidateMoney(salePrice, 2, nameof(salePrice));
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 30)
        {
            throw new ArgumentException("O código do produto deve possuir entre 1 e 30 caracteres.", nameof(code));
        }

        return normalized;
    }

    private static string? NormalizeBarcode(string? barcode)
    {
        var normalized = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length is not (8 or 12 or 13 or 14) || normalized.Any(character => !char.IsAsciiDigit(character)))
        {
            throw new ArgumentException("O código de barras deve possuir 8, 12, 13 ou 14 dígitos.", nameof(barcode));
        }

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160)
        {
            throw new ArgumentException("O nome do produto deve possuir entre 1 e 160 caracteres.", nameof(name));
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalized?.Length > 1000)
        {
            throw new ArgumentException("A descrição do produto deve possuir no máximo 1000 caracteres.", nameof(description));
        }

        return normalized;
    }

    private static int ValidateCategory(int categoryId)
    {
        if (categoryId <= 0)
        {
            throw new ArgumentException("Informe uma categoria válida.", nameof(categoryId));
        }

        return categoryId;
    }

    private static decimal ValidateMoney(decimal value, int scale, string parameterName)
    {
        if (value < 0 || value > 999_999_999_999.99m)
        {
            throw new ArgumentException("O valor deve estar entre zero e 999.999.999.999,99.", parameterName);
        }

        return decimal.Round(value, scale, MidpointRounding.AwayFromZero);
    }
}
