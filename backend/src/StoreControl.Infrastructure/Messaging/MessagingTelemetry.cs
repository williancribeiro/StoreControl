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
    private static readonly Histogram<double> PublicationDuration = Meter.CreateHistogram<double>(
        "storecontrol.messaging.publish.duration",
        "s");
    private static readonly Histogram<double> DeliveryDuration = Meter.CreateHistogram<double>(
        "storecontrol.messaging.consume.duration",
        "s");

    internal static void RecordPublication(string outcome, long started)
    {
        var tags = new TagList
        {
            { "messaging.event", MessagingContracts.SaleFinalizedType },
            { "messaging.outcome", outcome }
        };
        Publications.Add(1, tags);
        PublicationDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
    }

    internal static void RecordDelivery(string outcome, long started)
    {
        var tags = new TagList
        {
            { "messaging.consumer", MessagingContracts.SaleReceiptConsumer },
            { "messaging.outcome", outcome }
        };
        Deliveries.Add(1, tags);
        DeliveryDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
    }
}
