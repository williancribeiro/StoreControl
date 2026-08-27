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

Redis and RabbitMQ are running foundations only; this phase does not pretend to use them. Redis now requires authentication even on the local host. Images use explicit servicing/major-minor tags, every published port binds to 127.0.0.1, application containers prohibit privilege escalation, and separate edge/backend networks prevent Web from directly reaching the data services. Named volumes persist state.

The development Compose deliberately uses HTTP, SQL `sa` and the ASP.NET Core Development environment to support local Swagger. It must not be published as a production deployment. See [security.md](security.md).

## Latest validation

Compose configuration must be validated with a generated, ignored environment file:

    docker compose --env-file .env config --quiet

The previous container build/start remains blocked on the validation host because the Docker Desktop Linux engine pipe does not exist. SQL Server Express on the host was used for migrations, seed, API and integration tests.
