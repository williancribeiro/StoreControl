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

Redis adds `StoreControl.Cache` traces (`cache.get`, `cache.set`), the `storecontrol.cache.requests` event counter by region/outcome and `storecontrol.cache.redis.duration` histogram in milliseconds by operation. The outcomes distinguish hit, miss, fill, fallback, write failure, corrupt payload and disabled caching. Debug logs expose cache outcomes; Warning/Information logs capture unavailable/recovered transitions without credentials, payloads or raw keys. Optional OTLP configuration exports both traces and metrics. Tests observe emitted spans and measurements with real Redis.

`GET /health/cache` is separate from readiness and returns aggregate Healthy/Degraded without server details. Redis failure is degraded (HTTP 200); SQL failure still fails readiness. A disabled cache reports Healthy because SQL-only mode is intentional. See [caching.md](caching.md).

EF Core trace enrichment, RabbitMQ context propagation and local visualization services remain pending. No OTLP collector or dashboard UI is bundled or claimed as validated.
