# Docker

The root Compose file defines SQL Server, Redis, RabbitMQ, Migrator, API, Worker and Web.

Startup order:

    SQL Server healthy
    → DbMigrator applies migrations and seeds
    → API and Worker start
    → Web proxies /api to API

## Local secret initialization

Do not copy working values into `.env.example`. Generate the ignored `.env` before the first startup:

    .\scripts\Initialize-LocalEnvironment.ps1 -EnableDemoData

Omit `-EnableDemoData` when only reference roles, permissions and stores are desired. The script uses a cryptographic random-number generator for independent SQL Server, Redis, RabbitMQ, JWT and demo-account values. It refuses accidental overwrite unless `-Force` is explicit and never prints the generated values.

Required secret variables remain empty in `.env.example`; Compose interpolation fails before creating containers if SQL Server, Redis, RabbitMQ or JWT values are missing. Demo seed is false by default and its passwords are passed only to DbMigrator. The signing key is passed only to API. The Web container receives no token or password secret.

CookieSecure is false only in the local HTTP Compose environment; HTTPS deployments must leave it true. Changing generated values after SQL Server or RabbitMQ has initialized persistent volumes may not rotate credentials inside those services. For a disposable local reset, `docker compose down --volumes` removes all StoreControl container data before generating a new `.env`.

API uses authenticated Redis for dashboard/category cache and authenticated RabbitMQ for confirmed Outbox publication. Worker uses RabbitMQ for the sale-event consumer and SQL Server for Inbox/audit persistence. Redis has a 128 MB dataset limit, a 256 MB container limit and `allkeys-lru` eviction; snapshots and AOF are disabled because SQL owns the data. Redis no longer mounts a persistent volume. Any old `redis-data` volume is left untouched and can be removed separately after inspection. SQL Server and RabbitMQ retain their persistent volumes.

API does not wait for Redis readiness: a cold or unavailable Redis uses SQL fallback and reconnects automatically. It also does not wait for RabbitMQ: sales remain available through the SQL Outbox during broker outages. Worker waits for RabbitMQ health because it has no useful workload without the broker and does not depend on Redis. `MESSAGING_ENABLED=false` intentionally disables dispatcher and consumer; the topology prefix can isolate a local experiment. Images use explicit servicing/major-minor tags, every published port binds to 127.0.0.1, application containers prohibit privilege escalation, and separate edge/backend networks prevent Web from directly reaching the data services.

`CACHE_ENABLED` defaults to true in Compose and the local environment loader. Set it to false to compare uncached requests. `CACHE_KEY_PREFIX`, `CACHE_DASHBOARD_TTL_SECONDS`, `CACHE_CATEGORIES_TTL_SECONDS` and `CACHE_TIMEOUT_MILLISECONDS` have safe defaults. API receives `Cache__RedisConnection` using the generated `REDIS_PASSWORD`; Web and Worker do not receive Redis credentials. See [caching.md](caching.md) for the complete contract.

The development Compose deliberately uses HTTP, SQL `sa` and the ASP.NET Core Development environment to support local Swagger. It must not be published as a production deployment. See [security.md](security.md).

## Latest validation

Compose configuration must be validated with a generated, ignored environment file:

    docker compose --env-file .env config --quiet

Compose interpolation and syntax passed on 2026-09-04. Container build/start is still blocked: Docker Desktop fails during `initializing Ingest server` because it cannot remove/access `AppData/Local/Docker/run/sailor-ingest.sock`, and the Linux engine pipe never becomes available. No Docker reset or volume deletion was attempted.

Real service validation uses SQL Server Express and an isolated, authenticated [Redis 8.2.9 Windows community build](https://github.com/redis-windows/redis-windows/releases/tag/8.2.9), downloaded under ignored `work/` with the published SHA-256 verified. This is a validation fallback, not a new project dependency or proof that the Linux container runtime passed. CI uses the official Redis Linux image. Rerun `docker compose up --build` when the Docker engine is available.
