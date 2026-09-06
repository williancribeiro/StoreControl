using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StoreControl.Application.Abstractions.Persistence;

namespace StoreControl.Infrastructure.HealthChecks;

internal sealed class SqlServerHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var telemetry = DependencyHealthTelemetry.Start("sqlserver");
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var smokeCheck = scope.ServiceProvider.GetRequiredService<IDatabaseSmokeCheck>();
            var result = await smokeCheck.CheckAsync(cancellationToken);

            var health = HealthCheckResult.Healthy(
                "SQL Server disponível.",
                new Dictionary<string, object>
                {
                    ["foundationVersion"] = result.FoundationVersion,
                    ["serverUtcTime"] = result.ServerUtcTime
                });
            telemetry.Complete(health.Status);
            return health;
        }
        catch (Exception exception)
        {
            var health = HealthCheckResult.Unhealthy("SQL Server indisponível.", exception);
            telemetry.Complete(health.Status);
            return health;
        }
    }
}
