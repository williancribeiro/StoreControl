using System.Diagnostics;
using System.Text.Json;
using StoreControl.Application.Messaging;
using StoreControl.Domain.Sales;

namespace StoreControl.Infrastructure.Messaging;

internal static class MessagingContracts
{
    public const string SaleFinalizedType = "sale.finalized.v1";
    public const string SaleFinalizedRoutingKey = "sale.finalized.v1";
    public const string SaleReceiptConsumer = "sale-receipt.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public static OutboxMessage CreateSaleFinalized(Sale sale, string correlationId)
    {
        var finalizedAtUtc = sale.FinalizedAtUtc
            ?? throw new InvalidOperationException("Only finalized sales can produce an integration event.");
        var integrationEvent = new SaleFinalizedIntegrationEvent(
            sale.OperationId,
            1,
            sale.Id,
            sale.OperationId,
            sale.StoreId,
            sale.CustomerId,
            sale.TotalAmount,
            finalizedAtUtc);

        var activity = Activity.Current;
        var hasW3CContext = activity?.IdFormat == ActivityIdFormat.W3C
            && ActivityContext.TryParse(activity.Id, activity.TraceStateString, out _);

        return new OutboxMessage(
            integrationEvent.EventId,
            SaleFinalizedType,
            SaleFinalizedRoutingKey,
            JsonSerializer.Serialize(integrationEvent, SerializerOptions),
            correlationId,
            integrationEvent.FinalizedAtUtc,
            hasW3CContext ? activity!.Id : null,
            hasW3CContext ? activity!.TraceStateString : null);
    }

    public static SaleFinalizedIntegrationEvent DeserializeSaleFinalized(ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<SaleFinalizedIntegrationEvent>(body, SerializerOptions)
        ?? throw new JsonException("The sale finalized message payload is null.");
}
