namespace StoreControl.Application.Abstractions.Persistence;

public interface IDatabaseSmokeCheck
{
    Task<DatabaseSmokeResult> CheckAsync(CancellationToken cancellationToken);
}

public sealed record DatabaseSmokeResult(
    string Status,
    DateTime ServerUtcTime,
    string FoundationVersion);
