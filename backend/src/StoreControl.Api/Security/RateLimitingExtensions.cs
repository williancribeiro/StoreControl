using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace StoreControl.Api.Security;

public static class RateLimitPolicyNames
{
    public const string Authentication = "authentication";
}

public static class RateLimitingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddStoreControlRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection("RateLimiting:Authentication")
            .Get<AuthenticationRateLimitSettings>() ?? new AuthenticationRateLimitSettings();

        if (settings.PermitLimit is < 1 or > 1_000 || settings.WindowSeconds is < 1 or > 3_600)
        {
            throw new InvalidOperationException("Authentication rate-limit configuration is invalid.");
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicyNames.Authentication, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = settings.PermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds)
                    }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Muitas tentativas de autenticação.",
                    Detail = "Aguarde antes de tentar novamente.",
                    Type = "https://httpstatuses.com/429"
                };
                problem.Extensions["errorCode"] = "authentication_rate_limit_exceeded";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(problem, JsonOptions),
                    cancellationToken);
            };
        });

        return services;
    }

    private sealed class AuthenticationRateLimitSettings
    {
        public int PermitLimit { get; init; } = 10;

        public int WindowSeconds { get; init; } = 60;
    }
}
