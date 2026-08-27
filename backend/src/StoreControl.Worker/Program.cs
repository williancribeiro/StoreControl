using Serilog;
using Serilog.Formatting.Compact;
using StoreControl.Infrastructure;
using StoreControl.Worker;

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
    builder.Services.AddHostedService<FoundationWorker>();

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
