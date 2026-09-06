using System.Text;
using System.Diagnostics;
using RabbitMQ.Client;

namespace StoreControl.Infrastructure.Messaging;

internal static class RabbitMessageHeaders
{
    public static int RetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers?.TryGetValue("x-retry-count", out var value) != true)
        {
            return 0;
        }

        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number when number is >= 0 and <= int.MaxValue => (int)number,
            _ => throw new PermanentMessageException("The x-retry-count header is invalid.")
        };
    }

    public static ActivityContext ParentContext(IReadOnlyBasicProperties properties)
    {
        var traceParent = Utf8Header(properties, "traceparent");
        var traceState = Utf8Header(properties, "tracestate");
        return ParseParentContext(traceParent, traceState, isRemote: true);
    }

    public static ActivityContext ParentContext(string? traceParent, string? traceState)
        => ParseParentContext(traceParent, traceState, isRemote: false);

    private static ActivityContext ParseParentContext(string? traceParent, string? traceState, bool isRemote)
    {
        if (!ActivityContext.TryParse(traceParent, traceState, out var context))
        {
            return default;
        }

        return new ActivityContext(
            context.TraceId,
            context.SpanId,
            context.TraceFlags,
            context.TraceState,
            isRemote);
    }

    private static string? Utf8Header(IReadOnlyBasicProperties properties, string name)
    {
        if (properties.Headers?.TryGetValue(name, out var value) != true)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            string text => text,
            _ => null
        };
    }
}
