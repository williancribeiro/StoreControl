using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using StoreControl.Application.Abstractions.Persistence;
using StoreControl.Infrastructure;
using StoreControl.Infrastructure.Persistence;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .WriteTo.Console(new CompactJsonFormatter()));
    builder.Services.AddInfrastructure(builder.Configuration);

    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<StoreControlDbContext>();

    Log.Information("Applying StoreControl database migrations");
    await dbContext.Database.MigrateAsync();
    Log.Information("StoreControl database migrations applied successfully");

    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    Log.Information("Applying StoreControl reference and demonstration data seed");
    await seeder.SeedAsync(CancellationToken.None);
    Log.Information("StoreControl seed applied successfully");
}
catch (Exception exception)
{
    Log.Fatal(exception, "StoreControl database migration failed");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
