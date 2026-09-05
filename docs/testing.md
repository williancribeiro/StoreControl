# Testing

## Unit tests

StoreControl.UnitTests currently verifies:

- Domain references no StoreControl infrastructure project;
- Application does not reference Infrastructure;
- Store normalizes values, changes active state and rejects invalid code;
- Category normalization and active-state changes;
- Product barcode/money validation and deterministic decimal rounding;
- Customer/supplier document, e-mail and active-state rules.
- StockBalance quantity precision, movement direction, before/after arithmetic, insufficient-stock rejection and low-stock calculation.
- PurchaseOrder money/quantity rounding, snapshots, lifecycle, partial/full receipts, duplicate receipt rejection and cancellation rules.
- Sale historical-price capture, multiple-item/payment calculation, discount/payment invariants and duplicate-item rejection.
- CashSession physical-cash calculation, signed manual/refund movements, close difference and closed-session rejection.

## Integration tests

StoreControl.IntegrationTests boots the real ASP.NET Core API and uses EF migrations against SQL Server. It creates a uniquely named StoreControl_IntegrationTests_* database and verifies the prefix before deleting it.

Coverage currently includes:

- migration discovery and migration from zero;
- successful Identity login;
- signed access token and HttpOnly SameSite refresh cookie;
- authentication responses marked as non-cacheable and per-source login rate limiting;
- protected endpoint returning Problem Details 401;
- authenticated smoke diagnostics, minimal public health payload and defensive response headers;
- safe correlation-ID acceptance and replacement of unsafe values;
- Vendedor receiving 403 on an Administrator write;
- Administrator creating a store;
- refresh rotation, replay detection and family revocation;
- user creation with a store role and immediate JWT invalidation after deactivation;
- stale Store rowversion returning 409.
- registration migration discovery and migration from zero;
- category/product create and link, decimal persistence, duplicate conflict, stale rowversion, inactivation/reactivation and active-category invariant;
- product seed search, active/category filters, sort and pagination;
- customer document normalization/duplicate conflict plus Vendedor/Administrador policy differences for customers and suppliers.
- inventory migration and seed integrity, signed ledger, atomic adjustment rollback, store scope, role authorization and minimum-stock concurrency;
- two concurrent stock exits sharing one rowversion, proving exactly one writer/movement succeeds;
- virtual zero positions for a newly created product and tracked creation on its first adjustment;
- SQL constraint rejection of a direct negative balance.
- purchase creation without stock effects;
- receipt stock entry plus idempotent replay without duplicated receipt or movement;
- invalid-status rejection and allowed pre-receipt cancellation;
- API purchase permissions and store scope;
- forced SQL unique-key failure proving atomic rollback of purchase status/items, receipts and every stock change.
- finalized sale with multiple products and payments, historical values, stock exits, cash effects and exact idempotent replay;
- insufficient-stock rollback with no sale, stock or cash side effect;
- two concurrent sales of the last item with exactly one successful commit;
- forced SQL failure proving rollback across sale, multiple stock changes and cash movements;
- opening/manual entry/manual exit/closing cash lifecycle and difference calculation;
- sales/cash permission policies and store-scoped register visibility.
- sale cancellation stock/cash restoration, exact idempotent replay, preserved history and rollback when a refund cannot be paid.
- transfer creation without stock effect, dispatch/receipt pairing, pre-dispatch cancellation, idempotent transitions, both-store scope and concurrent last-unit dispatch.
- dashboard/sales-report calculations from committed sales, exclusion of cancelled sales and stored-procedure ranking;
- reporting permission, store scope, maximum-period validation, view/procedure creation and supporting indexes.
- real Redis hit/miss/absolute TTL, malformed/missing/null payload repair, canceled/failed loader behavior and metrics/traces;
- durable SQL generations, rollback, direct SQL writes and a delayed cache fill racing with a committed update;
- warm dashboard after sales/cancellation/minimum/product changes, category filters/pagination/inactivation and store-scope/permission enforcement;
- HTTP fallback with Redis unavailable, independent SQL readiness, safe failed Redis writes and generation-based freshness after using a healthy connection again;
- full idempotent migration script execution twice on an empty SQL database, plus cache migration rollback/reapply.

Set STORECONTROL_TEST_DB_CONNECTION to a SQL Server connection with create/drop database permission. The connection's database name is replaced with a safe unique test name. `scripts/Set-LocalEnvironment.ps1` prepares this value from the ignored local `.env`. GitHub Actions starts SQL Server with a newly generated masked password for each run rather than storing a working credential in the workflow.

The full suite also requires `STORECONTROL_TEST_REDIS_CONNECTION` for an isolated real Redis server. The loader supplies it, and CI starts authenticated ephemeral Redis with a masked generated password. Cache integration tests fail explicitly when it is missing. All API factories use unique cache prefixes when Redis is configured, so existing business tests also run through cached reads. One test needs ACL SETUSER/DELUSER to create/remove its unique write-denied identity; use an isolated development/test Redis instance, not production. Entries expire naturally; tests never FLUSHDB or flush other prefixes. Unit tests additionally validate canonical keys, scope/date separation and configuration bounds.

This phase uses a real configured SQL Server rather than Testcontainers so tests can run on the validation host even while its Docker daemon is unavailable. Testcontainers remains appropriate for later suites when the local Docker blocker is repaired.

## Frontend validation

Current automated frontend gates are:

- TypeScript project build;
- Oxlint with zero warnings;
- Vite production build.

An automated browser suite is still deferred. Manual browser validation in the main-registration phase covered category/product/customer/supplier creation, linked product edit, inactivation/reactivation, search, combined filter and pagination through the running Vite/API/SQL Server path. The reporting phase loaded the real Vite/API/SQL Express login surface after the routing changes and confirmed a clean browser console; credential submission was not performed. Authenticated dashboard, reports, cancellation and transfer behavior is covered at the HTTP/SQL boundary. Direct SQL and integration assertions remain the authoritative persistence/transaction checks.
