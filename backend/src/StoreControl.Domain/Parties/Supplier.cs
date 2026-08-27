using System.Net.Mail;

namespace StoreControl.Domain.Parties;

public sealed class Supplier
{
    private Supplier()
    {
    }

    public Supplier(string code, string name, string? document, string? email, string? phone)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        Document = NormalizeDocument(document);
        Email = NormalizeEmail(email);
        Phone = NormalizePhone(phone);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Document { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Update(string code, string name, string? document, string? email, string? phone, bool isActive)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        Document = NormalizeDocument(document);
        Email = NormalizeEmail(email);
        Phone = NormalizePhone(phone);
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 30)
        {
            throw new ArgumentException("O código do fornecedor deve possuir entre 1 e 30 caracteres.", nameof(code));
        }

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160)
        {
            throw new ArgumentException("O nome do fornecedor deve possuir entre 1 e 160 caracteres.", nameof(name));
        }

        return normalized;
    }

    private static string? NormalizeDocument(string? document)
    {
        if (document?.Any(character =>
                !char.IsAsciiDigit(character) &&
                !char.IsWhiteSpace(character) &&
                character is not ('.' or '-' or '/')) == true)
        {
            throw new ArgumentException("O documento contém caracteres inválidos.", nameof(document));
        }

        var normalized = string.IsNullOrWhiteSpace(document)
            ? null
            : new string(document.Where(char.IsAsciiDigit).ToArray());
        if (normalized is not null && normalized.Length is not (11 or 14))
        {
            throw new ArgumentException("O documento deve possuir 11 ou 14 dígitos.", nameof(document));
        }

        return normalized;
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalized = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        if (normalized is not null && (normalized.Length > 254 || !MailAddress.TryCreate(normalized, out _)))
        {
            throw new ArgumentException("Informe um e-mail válido com no máximo 254 caracteres.", nameof(email));
        }

        return normalized;
    }

    private static string? NormalizePhone(string? phone)
    {
        var normalized = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (normalized?.Length > 30)
        {
            throw new ArgumentException("O telefone deve possuir no máximo 30 caracteres.", nameof(phone));
        }

        return normalized;
    }
}
