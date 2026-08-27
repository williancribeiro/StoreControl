# Docker

The root Compose file defines SQL Server, Redis, RabbitMQ, Migrator, API, Worker and Web.

Startup order:

    SQL Server healthy
    → DbMigrator applies migrations and seeds
    → API and Worker start
    → Web proxies /api to API

## Authentication configuration

.env.example includes only local demonstration values:

- JWT_SIGNING_KEY;
- SEED_DEMO_DATA;
- DEMO_ADMIN_PASSWORD;
- DEMO_USER_PASSWORD.

Compose passes seed values only to DbMigrator and the signing key only to API. The Web container receives no token or password secret. CookieSecure is false in the local HTTP Compose environment; HTTPS deployments must leave it true.

Redis and RabbitMQ are running foundations only; this phase does not pretend to use them. Images use explicit servicing/major-minor tags, local ports bind to 127.0.0.1 and named volumes persist state.

docker compose down --volumes removes all local StoreControl container data.

## Latest validation

docker compose --env-file .env.example config --quiet passes after adding auth variables. Container build/start remains blocked on the validation host because the Docker Desktop Linux engine pipe does not exist. SQL Server Express on the host was used for migrations, seed, API and integration tests.
