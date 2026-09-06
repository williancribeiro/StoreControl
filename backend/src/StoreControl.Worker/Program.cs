using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using StoreControl.Infrastructure;
using StoreControl.Infrastructure.Messaging;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSaleFinalizedConsumer();

    var telemetryBuilder = builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("StoreControl.Worker"))
        .WithTracing(tracing => tracing.AddSource(MessagingTelemetry.SourceName))
        .WithMetrics(metrics => metrics.AddMeter(MessagingTelemetry.SourceName));

    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
    {
        telemetryBuilder.WithTracing(tracing => tracing.AddOtlpExporter(options =>
            options.Endpoint = endpoint));
        telemetryBuilder.WithMetrics(metrics => metrics.AddOtlpExporter(options =>
            options.Endpoint = endpoint));
    }

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "StoreControl Worker terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
