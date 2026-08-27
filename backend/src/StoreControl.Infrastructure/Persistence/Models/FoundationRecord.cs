namespace StoreControl.Infrastructure.Persistence.Models;

internal sealed class FoundationRecord
{
    public required string Key { get; init; }

    public required string Value { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
