# ADR 0005: selective Redis cache with transactional SQL generations

Status: implemented in Prompt 9.

## Selection before implementation

The dashboard repeats a daily-sales view, a ranking procedure, low-stock count/list and seven-day evolution. Cache its complete read model for 30 seconds. Category lists are small, frequently reused in registration screens and relatively stable; cache only unsearched lists for 300 seconds. Include pagination, active filter and ordering in their keys.

Keep filtered category searches, product/price reads, operational inventory, authentication/authorization, cash and period reports on SQL. Their integrity, write frequency or large filter space does not justify caching in this phase. These are workload-based candidates, not a claim of measured production speedup.

## Consistency decision

SQL remains authoritative. Two `platform.CacheRevisions` rows hold random generations for dashboard and categories. Small DML triggers replace the affected generation inside the business transaction. Dashboard dependencies are sales, sale items, stock balances (including minimum), products and stores; category writes invalidate category lists. Receipt, adjustment, transfer and reversal invalidate through their existing stock writes.

Every cache read obtains the committed generation from SQL. Redis keys include it, a schema version and normalized filters; dashboard keys additionally include the exact authorized store set, resolved again before every lookup. Old keys become unreachable and expire by TTL. No Redis deletion, key scan or network call participates in a business transaction.

This extra indexed SQL lookup deliberately trades a small query and metadata write for reliable invalidation after Redis outages, API crashes and concurrent fills. A reader that started before a commit can finish with the earlier view, but cannot populate the new generation. Rollback also rolls back invalidation. SQL changes outside EF follow the same rule. SQL Server trigger tables must disable EF's direct OUTPUT clause.

Invalidation is conservative across stores. The dashboard generation row can become a write-contention point at higher volume; monitor it before introducing per-store generations. No distributed lock or background invalidation worker is required for this phase.

## Availability and security

Redis is optional: bounded connection/operation timeouts, no disconnected command backlog, SQL fallback on Redis failure, and no fallback on SQL or authorization failure. Cache data is expendable. Do not cache credentials, tokens, permissions or user responses. Use authenticated Redis with environment configuration and isolated application prefixes. SQL readiness does not depend on Redis.

## Verification

Verify hit/miss, TTL, filter/store separation, authorization before hits, transaction rollback, write invalidation, a delayed fill racing with a write, Redis failure/recovery and corrupt payloads. Use real SQL and Redis for integration; keep targeted unit checks for deterministic key/options behavior.

References: [StackExchange.Redis configuration](https://github.com/StackExchange/StackExchange.Redis/blob/main/docs/Configuration.md), [EF Core SQL Server trigger compatibility](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/misc).
