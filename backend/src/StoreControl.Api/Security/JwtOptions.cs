namespace StoreControl.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
}

public sealed class RefreshTokenCookieOptions
{
    public const string SectionName = "Authentication:RefreshToken";

    public bool CookieSecure { get; init; } = true;
}

public static class StoreControlClaimTypes
{
    public const string Permission = "permission";
    public const string SecurityStamp = "sst";
    public const string Store = "store";
    public const string StoreRole = "store_role";
}
