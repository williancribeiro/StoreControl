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
        if (!options.Value.Enabled)
        {
            return HealthCheckResult.Healthy("Messaging disabled.");
        }

        try
        {
            var rabbitConnection = await connection.GetAsync(cancellationToken);
            return rabbitConnection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("RabbitMQ connection is recovering.");
        }
        catch (Exception exception) when (exception is BrokerUnreachableException or IOException or TimeoutException)
        {
            return HealthCheckResult.Degraded(
                "RabbitMQ unavailable; sale processing remains available through the SQL Outbox.");
        }
    }
}
