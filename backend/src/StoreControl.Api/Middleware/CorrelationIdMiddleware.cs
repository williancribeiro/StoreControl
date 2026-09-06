using System.Diagnostics;
using Serilog.Context;

namespace StoreControl.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string TraceHeaderName = "X-Trace-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        var activity = Activity.Current;
        var traceId = activity?.TraceId.ToString();
        var spanId = activity?.SpanId.ToString();
        activity?.SetTag("storecontrol.correlation_id", correlationId);
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            if (!string.IsNullOrWhiteSpace(traceId))
            {
                context.Response.Headers[TraceHeaderName] = traceId;
            }

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("SpanId", spanId))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var candidate = context.Request.Headers[HeaderName].FirstOrDefault();

        return !string.IsNullOrWhiteSpace(candidate) &&
               candidate.Length <= 128 &&
               candidate.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            ? candidate
            : Guid.NewGuid().ToString("N");
    }
}
