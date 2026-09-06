namespace StoreControl.Application.Messaging;

public sealed record SaleFinalizedIntegrationEvent(
    Guid EventId,
    int Version,
    long SaleId,
    Guid SaleOperationId,
    int StoreId,
    int? CustomerId,
    decimal TotalAmount,
    DateTime FinalizedAtUtc);
