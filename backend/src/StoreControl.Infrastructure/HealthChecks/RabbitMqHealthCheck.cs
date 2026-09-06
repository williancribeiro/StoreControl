using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;
using StoreControl.Infrastructure.Messaging;

namespace StoreControl.Infrastructure.HealthChecks;

internal sealed class RabbitMqHealthCheck(
    RabbitMqConnection connection,
    IOptions<MessagingOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var telemetry = DependencyHealthTelemetry.Start("rabbitmq");
        if (!options.Value.Enabled)
        {
            var disabled = HealthCheckResult.Healthy("Messaging disabled.");
            telemetry.Complete(disabled.Status);
            return disabled;
        }

        try
        {
            var rabbitConnection = await connection.GetAsync(cancellationToken);
            var health = rabbitConnection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("RabbitMQ connection is recovering.");
            telemetry.Complete(health.Status);
            return health;
        }
        catch (Exception exception) when (exception is BrokerUnreachableException or IOException or TimeoutException)
        {
            var health = HealthCheckResult.Degraded(
                "RabbitMQ unavailable; sale processing remains available through the SQL Outbox.");
            telemetry.Complete(health.Status);
            return health;
        }
    }
}
