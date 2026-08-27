namespace StoreControl.Infrastructure.Identity;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid FamilyId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public string? CreatedByIp { get; set; }

    public string? RevokedByIp { get; set; }

    public string? UserAgent { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser User { get; set; } = null!;

    public RefreshToken? ReplacedByToken { get; set; }

    public bool IsExpired(DateTime utcNow) => ExpiresAtUtc <= utcNow;

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && !IsExpired(utcNow);
}
