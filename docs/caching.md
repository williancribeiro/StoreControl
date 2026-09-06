# Caching

Status: implemented in Prompt 9. SQL Server remains authoritative; Redis is an optional read optimization.

## Selected queries

| Query | Default absolute TTL | Why cache it? | Key inputs |
| --- | --- | --- | --- |
| `GET /api/dashboard` | 30 seconds | one recurring screen loads a daily-sales aggregate, seven-day evolution, top-product procedure and low-stock count/list | effective UTC date, requested store and sorted/distinct authorized store IDs |
| `GET /api/categories` without a nonblank search | 300 seconds | small, relatively stable lists reused by registration screens | page, page size, active filter, normalized sort field and direction |

Keys also contain the configured deployment prefix, serialization version `v1`, region and committed SQL generation. Filter inputs are SHA-256 hashed to keep keys bounded. The resolved date prevents a midnight request from reusing yesterday's default dashboard. Explicit-store and all-accessible-store responses have distinct keys even when the accessible set has one store.

Category search, product/price reads, operational stock, customers/suppliers, cash, authentication/authorization and period reports are not cached. Sales always load authoritative product prices and balances from SQL. Selection reflects repeated query work and the UI's access pattern; no production latency improvement is claimed without representative measurements.

## Read path and authorization

1. Authenticate and enforce the endpoint policy. Dashboard also resolves current SQL store assignments before every lookup.
2. Read the region's committed generation from the indexed `platform.CacheRevisions` primary key.
3. GET the Redis key. A valid entry is a **hit** and bypasses the selected aggregate/list queries, not authentication or SQL generation checks.
4. An absent/invalid entry is a **miss**. Execute the original SQL query and SET the serialized response with an absolute TTL. Hits never extend expiry.
5. Redis connection/read failure returns the result of the normal SQL path. A failed SET does not discard a successful SQL result.

Redis contains no access tokens, refresh tokens, credentials, permissions or user profiles. Controllers continue enforcing authorization for warm entries. These HTTP responses use `Cache-Control: no-store`; browser/proxy caching must not bypass server authorization. Backend Redis caching is independent of that HTTP directive.

## Transactional invalidation

`20260905004822_CacheGenerations` adds two random GUID generations. Six small SQL Server AFTER INSERT/UPDATE/DELETE triggers change the relevant generation inside the same transaction:

| Region | SQL dependencies | Covered operations |
| --- | --- | --- |
| dashboard | `sales.Sales`, `sales.SaleItems` | sale creation/finalization, cancellation and item history changes |
| dashboard | `inventory.StockBalances` | adjustments, minimum configuration, purchase receipts, transfer dispatch/receipt and sale/reversal stock effects |
| dashboard | `catalog.Products`, `organization.Stores` | names/codes, product/store lifecycle and other edits |
| categories | `catalog.Categories` | create, edit, inactivate, reactivate or a permitted direct SQL change |

Purchase creation or cancellation before receipt does not change dashboard inputs; it needs no cache invalidation. Receipt does through the balance trigger. Category edits do not change any field returned by the dashboard. Customer, supplier and manual cash changes likewise have no cache dependency.

Invalidation makes old keys unreachable rather than scanning/deleting Redis keys. TTL and eviction reclaim them. A delayed reader keeps the generation captured before its SQL load; it cannot fill the generation introduced by a concurrent commit. A read already in flight may finish with its earlier view, as with an ordinary concurrent database read. Requests resolving the generation after a completed write see the new generation. Multiple dashboard SQL queries do not constitute a snapshot-isolated report.

Rollback rolls back the generation together with the business write. SQL writes and seeding follow the triggers too, so Redis downtime, a failed network invalidation or an API crash cannot leave a committed change tied to the old generation. No Redis I/O occurs in a business transaction. EF disables direct OUTPUT on trigger-bearing tables while preserving identity/rowversion retrieval. Fixed trigger DDL remains visible in the migration.

The two generation rows must exist and dependency triggers must stay enabled; maintenance/import scripts must preserve this contract. After restoring an old SQL backup, change the cache prefix before reusing an existing Redis instance. No public cache-purge endpoint exists.

## Configuration

| Environment key for .NET | Default | Allowed values / meaning |
| --- | --- | --- |
| `Cache__Enabled` | false in application defaults; true in Compose/local loader | explicit opt-out for SQL-only comparison |
| `Cache__RedisConnection` | empty | StackExchange.Redis connection configuration; required when enabled; comes from local secrets |
| `Cache__KeyPrefix` | `storecontrol:local` | 1–80 letters, digits, colon, underscore or hyphen; unique per deployment |
| `Cache__DashboardTtlSeconds` | 30 | 1–300 seconds |
| `Cache__CategoriesTtlSeconds` | 300 | 1–3600 seconds |
| `Cache__TimeoutMilliseconds` | 500 | 100–5000 ms per Redis operation/connection attempt |

The `.env` equivalents are `CACHE_ENABLED`, `CACHE_KEY_PREFIX`, `CACHE_DASHBOARD_TTL_SECONDS`, `CACHE_CATEGORIES_TTL_SECONDS`, `CACHE_TIMEOUT_MILLISECONDS`, `REDIS_PORT` and the generated `REDIS_PASSWORD`. `Set-LocalEnvironment.ps1` maps them to the .NET variables without printing secrets and supplies defaults for older `.env` files. Do not regenerate existing credentials just to add cache settings.

## Redis unavailable or full

One lazy shared multiplexer reconnects in the background. Initial connection waiting is bounded by the configured timeout plus 250 ms; GET and SET each have their own timeout. Disconnected commands fail promptly instead of accumulating in a backlog. These limits are not a deadline for the complete SQL-backed request. Cancellation propagates rather than becoming fallback data.

Connection/timeouts and Redis command errors fall back to SQL. Missing required JSON constructor fields, malformed JSON and null payloads are reloaded; a successful fill replaces the invalid entry. SQL failures, authorization failures and source-load cancellation are never hidden by Redis fallback. No stale-data fallback is served when SQL is unavailable.

Compose uses 128 MB `maxmemory`, `allkeys-lru`, a 256 MB container limit, no AOF and no snapshots. Eviction or restarting Redis produces a cold cache with correct SQL results. API readiness depends on SQL, not Redis. `/health/cache` reports aggregate Healthy/Degraded (HTTP 200 for degraded optional cache), while `/health/ready` remains the SQL readiness gate.

## Observation and study

`StoreControl.Cache` emits `cache.get`/`cache.set` spans under the HTTP trace. The `storecontrol.cache.requests` counter uses bounded `cache.region` and `cache.outcome` labels: `hit`, `miss`, `fill`, `fallback`, `write_failure`, `corrupt`, `disabled`. Outcomes count events: a miss and its fill are two events, not two HTTP requests. `storecontrol.cache.redis.duration` records milliseconds by get/set operation. No user ID, raw filter, Redis key, password or payload is a metric label or log field.

Hit/miss details are Debug logs. An unavailable transition produces one Warning with the exception type, and a successful cache operation logs recovery once. `OpenTelemetry__OtlpEndpoint` exports both metrics and traces when configured; the local Compose supplies the loopback Aspire Dashboard described in [observability.md](observability.md).

For manual inspection with Compose, open an authenticated CLI without printing the password:

    docker compose exec redis sh -c 'REDISCLI_AUTH="$REDIS_PASSWORD" redis-cli'

Then use `SCAN 0 MATCH storecontrol:local:v1:* COUNT 100`, `TTL <observed-key>` and `INFO stats`. Avoid `KEYS *`, `MONITOR`, `FLUSHDB` or shared-cache deletion as routine diagnostics. See [debugging-map.md](debugging-map.md).

## Tradeoffs and tests

The global dashboard generation deliberately invalidates all store/date variants. This is simple for the current store count but creates a shared write-lock point and can reduce hit rate during heavy sales activity. Existing transactional retry/concurrency tests pass with the triggers. Measure SQL lock waits, read latency, Redis memory and hit rate before adding per-store generations. Cache misses are not coalesced; simultaneous cold requests may repeat SQL work. No distributed lock, extra worker or process-local data cache is introduced.

Tests use real SQL Server and authenticated Redis, unique key prefixes and expiring entries. They cover hit/miss/TTL, JSON corruption, rollback, direct SQL invalidation, delayed fill versus commit, scope/permission checks, warm sale/cancellation/minimum/category changes, Redis outage fallback, HTTP health behavior, denied cache writes and cancellation. `STORECONTROL_TEST_REDIS_CONNECTION` is required for the full suite; cache tests fail explicitly when it is missing. The test-only Redis identity used to deny writes is removed after the test. No tests flush a shared Redis database.

The implementation decision and official API references are in [ADR 0005](adr/0005-selective-redis-cache.md). Local validation limitations are in [project-state.md](project-state.md).
