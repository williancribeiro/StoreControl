using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace StoreControl.Infrastructure.HealthChecks;

public static class DependencyHealthTelemetry
{
    public const string SourceName = "StoreControl.Dependencies";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Checks = Meter.CreateCounter<long>(
        "storecontrol.dependencies.health_checks",
        "{check}");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "storecontrol.dependencies.health_check.duration",
        "s");

    internal static CheckOperation Start(string dependency) => new(dependency);

    internal sealed class CheckOperation(string dependency) : IDisposable
    {
        private readonly Activity? _activity = StartActivity(dependency);
        private readonly long _started = Stopwatch.GetTimestamp();
        private bool _completed;

        internal void Complete(HealthStatus status)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            var outcome = status.ToString().ToLowerInvariant();
            _activity?.SetTag("storecontrol.dependency.status", outcome);
            _activity?.SetStatus(status == HealthStatus.Unhealthy
                ? ActivityStatusCode.Error
                : ActivityStatusCode.Ok);
            var tags = new TagList
            {
                { "dependency.name", dependency },
                { "dependency.status", outcome }
            };
            Checks.Add(1, tags);
            Duration.Record(Stopwatch.GetElapsedTime(_started).TotalSeconds, tags);
        }

        public void Dispose()
        {
            _activity?.Dispose();
        }

        private static Activity? StartActivity(string dependency)
        {
            var activity = Activities.StartActivity("dependency.health.check", ActivityKind.Internal);
            activity?.SetTag("storecontrol.dependency.name", dependency);
            return activity;
        }
    }
}
