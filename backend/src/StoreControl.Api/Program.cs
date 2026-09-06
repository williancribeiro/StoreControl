using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using StoreControl.Api.ErrorHandling;
using StoreControl.Api.HealthChecks;
using StoreControl.Api.Middleware;
using StoreControl.Api.Security;
using StoreControl.Infrastructure;
using StoreControl.Infrastructure.Caching;
using StoreControl.Infrastructure.HealthChecks;
using StoreControl.Infrastructure.Messaging;
using StoreControl.Infrastructure.Sales;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    Activity.ForceDefaultIdFormat = true;

    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString();
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(
            serviceName: "StoreControl.Api",
            serviceNamespace: "StoreControl",
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .AddAttributes([
            new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName)
        ]);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
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

    builder.Services.AddControllers();
    builder.Services.AddStoreControlSecurity(builder.Configuration);
    builder.Services.AddStoreControlRateLimiting(builder.Configuration);
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] =
                Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
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
                    .AllowCredentials()
                    .WithExposedHeaders(
                        CorrelationIdMiddleware.HeaderName,
                        CorrelationIdMiddleware.TraceHeaderName);
            }
        });
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddOutboxPublisher();

    var telemetryBuilder = builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: "StoreControl.Api",
            serviceNamespace: "StoreControl",
            serviceVersion: serviceVersion,
            serviceInstanceId: Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment.name", builder.Environment.EnvironmentName)
            ]))
        .WithTracing(tracing => tracing
            .AddSource(CacheTelemetry.SourceName)
            .AddSource(BusinessTelemetry.SourceName)
            .AddSource(DependencyHealthTelemetry.SourceName)
            .AddSource(MessagingTelemetry.SourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddMeter(CacheTelemetry.SourceName)
            .AddMeter(BusinessTelemetry.SourceName)
            .AddMeter(DependencyHealthTelemetry.SourceName)
            .AddMeter(MessagingTelemetry.SourceName)
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation());

    if (otlpEndpoint is not null)
    {
        telemetryBuilder.WithTracing(tracing => tracing.AddOtlpExporter(options =>
            options.Endpoint = otlpEndpoint));
        telemetryBuilder.WithMetrics(metrics => metrics.AddOtlpExporter(options =>
            options.Endpoint = otlpEndpoint));
    }

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, _, exception) => exception is not null
            || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
                    ? LogEventLevel.Warning
                    : httpContext.Request.Path.StartsWithSegments("/health")
                        ? LogEventLevel.Debug
                        : LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("Endpoint", httpContext.GetEndpoint()?.DisplayName ?? "unmatched");
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                diagnosticContext.Set("UserId", userId);
            }
        };
    });
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
