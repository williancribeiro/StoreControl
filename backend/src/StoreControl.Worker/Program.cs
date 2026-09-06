using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
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
    Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    Activity.ForceDefaultIdFormat = true;

    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString();
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(
            serviceName: "StoreControl.Worker",
            serviceNamespace: "StoreControl",
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .AddAttributes([
            new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName)
        ]);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    var otlpEndpointValue = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    Uri? otlpEndpoint = null;
    if (!string.IsNullOrWhiteSpace(otlpEndpointValue)
        && !Uri.TryCreate(otlpEndpointValue, UriKind.Absolute, out otlpEndpoint))
    {
        throw new InvalidOperationException("OpenTelemetry:OtlpEndpoint must be an absolute URI when configured.");
    }

    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeScopes = true;
        logging.IncludeFormattedMessage = true;
        logging.SetResourceBuilder(resourceBuilder);
        if (otlpEndpoint is not null)
        {
            logging.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
        }
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSaleFinalizedConsumer();

    var telemetryBuilder = builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: "StoreControl.Worker",
            serviceNamespace: "StoreControl",
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName)
            ]))
        .WithTracing(tracing => tracing
            .AddSource(MessagingTelemetry.SourceName)
            .AddSqlClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddMeter(MessagingTelemetry.SourceName)
            .AddRuntimeInstrumentation());

    if (otlpEndpoint is not null)
    {
        telemetryBuilder.WithTracing(tracing => tracing.AddOtlpExporter(options =>
            options.Endpoint = otlpEndpoint));
        telemetryBuilder.WithMetrics(metrics => metrics.AddOtlpExporter(options =>
            options.Endpoint = otlpEndpoint));
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
