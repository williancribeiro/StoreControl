using System.Diagnostics;
using System.Diagnostics.Metrics;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Sales;

namespace StoreControl.Infrastructure.Sales;

public static class BusinessTelemetry
{
    public const string SourceName = "StoreControl.Business";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> SaleFinalizations = Meter.CreateCounter<long>(
        "storecontrol.sales.finalizations",
        "{sale}");
    private static readonly Histogram<double> SaleTotal = Meter.CreateHistogram<double>(
        "storecontrol.sales.total",
        "BRL");
    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "storecontrol.business.operation.duration",
        "s");

    internal static SaleFinalizationOperation StartSaleFinalization(Guid operationId, int storeId) =>
        new(operationId, storeId);

    internal sealed class SaleFinalizationOperation : IDisposable
    {
        private readonly Activity? _activity;
        private readonly long _started = Stopwatch.GetTimestamp();
        private bool _completed;

        internal SaleFinalizationOperation(Guid operationId, int storeId)
        {
            _activity = Activities.StartActivity("sale.finalize", ActivityKind.Internal);
            _activity?.SetTag("storecontrol.sale.operation_id", operationId);
            _activity?.SetTag("storecontrol.store.id", storeId);
        }

        internal void Complete(FinalizeSaleResult result)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            var outcome = result.WasAlreadyProcessed ? "replayed" : "committed";
            _activity?.SetTag("storecontrol.sale.id", result.Sale.Id);
            _activity?.SetTag("storecontrol.sale.total", result.Sale.TotalAmount);
            _activity?.SetTag("storecontrol.sale.replayed", result.WasAlreadyProcessed);
            _activity?.SetStatus(ActivityStatusCode.Ok);
            Record(outcome);
            if (!result.WasAlreadyProcessed)
            {
                SaleTotal.Record(decimal.ToDouble(result.Sale.TotalAmount));
            }
        }

        internal void Fail(Exception exception)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            var outcome = Outcome(exception);
            _activity?.SetTag("error.type", exception.GetType().FullName);
            _activity?.SetStatus(ActivityStatusCode.Error);
            Record(outcome);
        }

        private void Record(string outcome)
        {
            SaleFinalizations.Add(
                1,
                new KeyValuePair<string, object?>("business.outcome", outcome));
            OperationDuration.Record(
                Stopwatch.GetElapsedTime(_started).TotalSeconds,
                new("business.operation", "sale.finalize"),
                new("business.outcome", outcome));
        }

        public void Dispose()
        {
            if (!_completed)
            {
                _activity?.SetStatus(ActivityStatusCode.Unset);
            }

            _activity?.Dispose();
        }

        private static string Outcome(Exception exception) => exception switch
        {
            OperationCanceledException => "canceled",
            RequestValidationException => "validation_failed",
            ResourceConflictException => "conflict",
            ResourceNotFoundException => "not_found",
            _ => "failed"
        };
    }
}
