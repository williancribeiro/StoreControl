using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Abstractions.Persistence;

namespace StoreControl.Infrastructure.Persistence;

internal sealed class SqlServerDatabaseSmokeCheck(StoreControlDbContext dbContext)
    : IDatabaseSmokeCheck
{
    public async Task<DatabaseSmokeResult> CheckAsync(CancellationToken cancellationToken)
    {
        var foundationVersion = await dbContext.FoundationRecords
            .AsNoTracking()
            .Where(record => record.Key == "foundation-version")
            .Select(record => record.Value)
            .SingleAsync(cancellationToken);

        var serverTime = await dbContext.Database
            .SqlQueryRaw<DateTime>("SELECT SYSUTCDATETIME() AS [Value]")
            .SingleAsync(cancellationToken);

        return new DatabaseSmokeResult(
            "connected",
            DateTime.SpecifyKind(serverTime, DateTimeKind.Utc),
            foundationVersion);
    }
}
