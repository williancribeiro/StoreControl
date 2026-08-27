using Serilog.Context;

namespace StoreControl.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
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
