using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using StoreControl.Api.ErrorHandling;
using StoreControl.Api.HealthChecks;
using StoreControl.Api.Middleware;
using StoreControl.Api.Security;
using StoreControl.Infrastructure;
using StoreControl.Infrastructure.Caching;
using StoreControl.Infrastructure.Messaging;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddStoreControlSecurity(builder.Configuration);
    builder.Services.AddStoreControlRateLimiting(builder.Configuration);
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        };
    });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "StoreControl API",
            Version = "v1",
            Description = "API do laboratório empresarial StoreControl."
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Informe o access token JWT."
        });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    });

    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("frontend", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        });
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddOutboxPublisher();

    var telemetryBuilder = builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("StoreControl.Api"))
        .WithTracing(tracing => tracing
            .AddSource(CacheTelemetry.SourceName)
            .AddSource(MessagingTelemetry.SourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddMeter(CacheTelemetry.SourceName)
            .AddMeter(MessagingTelemetry.SourceName));

    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var endpoint))
    {
        telemetryBuilder.WithTracing(tracing => tracing.AddOtlpExporter(options =>
            options.Endpoint = endpoint));
        telemetryBuilder.WithMetrics(metrics => metrics.AddOtlpExporter(options =>
            options.Endpoint = endpoint));
    }

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseRouting();
    app.UseCors("frontend");
    app.UseRateLimiter();
    app.UseAuthentication();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "StoreControl API v1");
            options.DisplayRequestDuration();
        });
    }

    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health/cache", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("cache"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });
    app.MapHealthChecks("/health/messaging", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("messaging"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    });

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "StoreControl API terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
