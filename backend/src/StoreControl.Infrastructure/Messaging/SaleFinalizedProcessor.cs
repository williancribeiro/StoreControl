using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StoreControl.Domain.Sales;
using StoreControl.Infrastructure.Persistence;

namespace StoreControl.Infrastructure.Messaging;

internal enum SaleFinalizedProcessingOutcome
{
    SimulatedSent,
    SkippedNoDestination,
    Duplicate
}

internal sealed class SaleFinalizedProcessor(StoreControlDbContext dbContext)
{
    public async Task<SaleFinalizedProcessingOutcome> ProcessAsync(
        Guid messageId,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var bodyCopy = body.ToArray();
        var payloadHash = SHA256.HashData(bodyCopy);
        var integrationEvent = DeserializeAndValidate(messageId, bodyCopy);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => ProcessOnceAsync(
            messageId,
            payloadHash,
            integrationEvent,
            cancellationToken));
    }

    private async Task<SaleFinalizedProcessingOutcome> ProcessOnceAsync(
        Guid messageId,
        byte[] payloadHash,
        Application.Messaging.SaleFinalizedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        // A retried execution may reuse this scoped DbContext after an uncertain commit.
        // Clearing state makes the Inbox row in SQL the source of truth for the next attempt.
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await dbContext.InboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Consumer == MessagingContracts.SaleReceiptConsumer
                    && item.MessageId == messageId,
                cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(existing.PayloadHash, payloadHash))
            {
                throw new MessageIdentityCollisionException(messageId);
            }

            await transaction.CommitAsync(cancellationToken);
            return SaleFinalizedProcessingOutcome.Duplicate;
        }

        var sale = await dbContext.Sales
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == integrationEvent.SaleId, cancellationToken)
            ?? throw new PermanentMessageException(
                $"Sale {integrationEvent.SaleId} referenced by message {messageId:D} does not exist.");

        ValidateAgainstSale(integrationEvent, sale);

        var destination = integrationEvent.CustomerId.HasValue
            ? await dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.Id == integrationEvent.CustomerId.Value)
                .Select(customer => customer.Email)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var processedAt = DateTime.UtcNow;
        var recipientHash = string.IsNullOrWhiteSpace(destination)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes(destination));
        var status = recipientHash is null
            ? SaleReceiptDeliveryStatus.SkippedNoDestination
            : SaleReceiptDeliveryStatus.SimulatedSent;

        dbContext.SaleReceiptDeliveries.Add(new SaleReceiptDelivery(
            messageId,
            integrationEvent.SaleId,
            status,
            recipientHash,
            processedAt));
        dbContext.InboxMessages.Add(new InboxMessage(
            MessagingContracts.SaleReceiptConsumer,
            messageId,
            payloadHash,
            processedAt));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return recipientHash is null
            ? SaleFinalizedProcessingOutcome.SkippedNoDestination
            : SaleFinalizedProcessingOutcome.SimulatedSent;
    }

    private static Application.Messaging.SaleFinalizedIntegrationEvent DeserializeAndValidate(
        Guid messageId,
        ReadOnlySpan<byte> body)
    {
        Application.Messaging.SaleFinalizedIntegrationEvent integrationEvent;
        try
        {
            integrationEvent = MessagingContracts.DeserializeSaleFinalized(body);
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new PermanentMessageException("The sale finalized payload is malformed.", exception);
        }

        if (integrationEvent.Version != 1
            || integrationEvent.EventId != messageId
            || integrationEvent.EventId == Guid.Empty
            || integrationEvent.SaleId <= 0
            || integrationEvent.SaleOperationId == Guid.Empty
            || integrationEvent.StoreId <= 0
            || integrationEvent.CustomerId <= 0
            || integrationEvent.TotalAmount <= 0
            || integrationEvent.FinalizedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new PermanentMessageException("The sale finalized payload contains invalid or unsupported values.");
        }

        return integrationEvent;
    }

    private static void ValidateAgainstSale(
        Application.Messaging.SaleFinalizedIntegrationEvent integrationEvent,
        Sale sale)
    {
        var finalizedAt = sale.FinalizedAtUtc;
        var timestampDifference = finalizedAt.HasValue
            ? (finalizedAt.Value - integrationEvent.FinalizedAtUtc).Duration()
            : TimeSpan.MaxValue;
        if (sale.Status is not (SaleStatus.Finalized or SaleStatus.Cancelled)
            || sale.OperationId != integrationEvent.SaleOperationId
            || sale.StoreId != integrationEvent.StoreId
            || sale.CustomerId != integrationEvent.CustomerId
            || sale.TotalAmount != integrationEvent.TotalAmount
            || timestampDifference >= TimeSpan.FromMilliseconds(1))
        {
            throw new PermanentMessageException(
                $"Message data does not match authoritative sale {integrationEvent.SaleId}.");
        }
    }
}
