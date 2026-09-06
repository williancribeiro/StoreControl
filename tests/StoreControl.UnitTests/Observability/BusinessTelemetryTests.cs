using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Infrastructure.HealthChecks;
using StoreControl.Infrastructure.Sales;

namespace StoreControl.UnitTests.Observability;

public sealed class BusinessTelemetryTests
{
    [Fact]
    public void Sale_failure_records_a_bounded_outcome_and_marks_the_activity_as_error()
    {
        Activity? stoppedActivity = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BusinessTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(activityListener);

        var measurements = new ConcurrentBag<(string Instrument, string? Outcome)>();
        using var meterListener = CreateMeterListener(BusinessTelemetry.SourceName, measurements);

        using (var operation = BusinessTelemetry.StartSaleFinalization(Guid.NewGuid(), 3))
        {
            operation.Fail(new ResourceConflictException("expected test conflict"));
        }

        Assert.NotNull(stoppedActivity);
        Assert.Equal("sale.finalize", stoppedActivity.OperationName);
        Assert.Equal(ActivityStatusCode.Error, stoppedActivity.Status);
        Assert.Equal("conflict", Assert.Single(
            measurements,
            measurement => measurement.Instrument == "storecontrol.sales.finalizations").Outcome);
        Assert.Contains(measurements, measurement =>
            measurement.Instrument == "storecontrol.business.operation.duration"
            && measurement.Outcome == "conflict");
    }

    [Fact]
    public void Dependency_check_records_status_without_connection_details()
    {
        var measurements = new ConcurrentBag<(string Instrument, string? Outcome)>();
        using var meterListener = CreateMeterListener(DependencyHealthTelemetry.SourceName, measurements);

        using (var operation = DependencyHealthTelemetry.Start("redis"))
        {
            operation.Complete(HealthStatus.Degraded);
        }

        Assert.Contains(measurements, measurement =>
            measurement.Instrument == "storecontrol.dependencies.health_checks"
            && measurement.Outcome == "degraded");
        Assert.DoesNotContain(measurements, measurement => measurement.Outcome?.Contains("localhost") == true);
    }

    private static MeterListener CreateMeterListener(
        string meterName,
        ConcurrentBag<(string Instrument, string? Outcome)> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, FindOutcome(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, FindOutcome(tags))));
        listener.Start();
        return listener;
    }

    private static string? FindOutcome(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key is "business.outcome" or "dependency.status")
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }
}
