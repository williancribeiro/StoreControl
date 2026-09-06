using Microsoft.EntityFrameworkCore;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Messaging;

internal sealed class OutboxStore(StoreControlDbContext dbContext)
{
    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var lockId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var lockedUntil = now.Add(leaseDuration);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            ;WITH candidates AS
            (
                SELECT TOP ({batchSize}) *
                FROM [integration].[OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [PublishedAtUtc] IS NULL
                  AND [NextAttemptAtUtc] <= {now}
                  AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] < {now})
                ORDER BY [CreatedAtUtc], [Id]
            )
            UPDATE candidates
            SET [LockId] = {lockId},
                [LockedUntilUtc] = {lockedUntil},
                [PublishAttempts] = [PublishAttempts] + 1;
            """, cancellationToken);

            var messages = await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message => message.LockId == lockId)
                .OrderBy(message => message.CreatedAtUtc)
                .ThenBy(message => message.Id)
                .ToListAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (IReadOnlyList<OutboxMessage>)messages;
        });
    }

    public Task MarkPublishedAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        dbContext.OutboxMessages
            .Where(candidate => candidate.Id == message.Id && candidate.LockId == message.LockId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.PublishedAtUtc, DateTime.UtcNow)
                .SetProperty(candidate => candidate.LockId, (Guid?)null)
                .SetProperty(candidate => candidate.LockedUntilUtc, (DateTime?)null)
                .SetProperty(candidate => candidate.LastError, (string?)null), cancellationToken);

    public Task MarkFailedAsync(
        OutboxMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var exponent = Math.Min(Math.Max(message.PublishAttempts - 1, 0), 6);
        var nextAttempt = DateTime.UtcNow.AddSeconds(Math.Pow(2, exponent));
        var error = $"{exception.GetType().Name}: {exception.Message}";
        if (error.Length > 1_000)
        {
            error = error[..1_000];
        }

        return dbContext.OutboxMessages
            .Where(candidate => candidate.Id == message.Id && candidate.LockId == message.LockId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.NextAttemptAtUtc, nextAttempt)
                .SetProperty(candidate => candidate.LockId, (Guid?)null)
                .SetProperty(candidate => candidate.LockedUntilUtc, (DateTime?)null)
                .SetProperty(candidate => candidate.LastError, error), cancellationToken);
    }
}
