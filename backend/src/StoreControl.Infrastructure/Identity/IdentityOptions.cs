namespace StoreControl.Infrastructure.Identity;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "Authentication:RefreshToken";

    public int LifetimeDays { get; init; } = 7;
}

public sealed class DemoSeedOptions
{
    public const string SectionName = "Seed";

    public bool DemoDataEnabled { get; init; }

    public string AdminPassword { get; init; } = string.Empty;

    public string UserPassword { get; init; } = string.Empty;
}
