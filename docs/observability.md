# Observability

The current foundation provides:

- compact JSON logs through Serilog;
- request logging without method-entry noise;
- validated `X-Correlation-ID` acceptance/generation and response propagation;
- ASP.NET Core and outgoing HTTP tracing through OpenTelemetry;
- optional OTLP export configured by `OpenTelemetry:OtlpEndpoint`;
- liveness and SQL Server readiness checks whose anonymous payload exposes only aggregate status;
- authenticated foundation smoke details for developers who need SQL time and schema version.

Committed sales log sale ID, store and total only after SQL commit. Cash opening, manual movement and closing log their durable identifiers. Expected validation/conflict paths remain correlated Problem Details instead of method-entry noise. Payment references, credentials and tokens must not be logged.

Metrics, EF Core trace enrichment, RabbitMQ context propagation and local visualization services remain pending until their producing modules exist.
