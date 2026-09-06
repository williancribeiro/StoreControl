using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace StoreControl.Infrastructure.Messaging;

public static class MessagingTelemetry
{
    public const string SourceName = "StoreControl.Messaging";
    internal static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Publications = Meter.CreateCounter<long>("storecontrol.messaging.publications");
    private static readonly Counter<long> Deliveries = Meter.CreateCounter<long>("storecontrol.messaging.deliveries");

    internal static void RecordPublication(string outcome) =>
        Publications.Add(1, new("messaging.event", MessagingContracts.SaleFinalizedType), new("messaging.outcome", outcome));

    internal static void RecordDelivery(string outcome) =>
        Deliveries.Add(1, new("messaging.consumer", MessagingContracts.SaleReceiptConsumer), new("messaging.outcome", outcome));
}
