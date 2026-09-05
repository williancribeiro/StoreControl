namespace StoreControl.Infrastructure.Persistence.Models;

internal sealed class CacheRevision
{
    public string Name { get; private set; } = string.Empty;

    public Guid Generation { get; private set; }
}
