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

Set STORECONTROL_TEST_DB_CONNECTION to a SQL Server connection with create/drop database permission. The connection's database name is replaced with a safe unique test name. `scripts/Set-LocalEnvironment.ps1` prepares this value from the ignored local `.env`. GitHub Actions starts SQL Server with a newly generated masked password for each run rather than storing a working credential in the workflow.

This phase uses a real configured SQL Server rather than Testcontainers so tests can run on the validation host even while its Docker daemon is unavailable. Testcontainers remains appropriate for later suites when the local Docker blocker is repaired.

## Frontend validation

Current automated frontend gates are:

- TypeScript project build;
- Oxlint with zero warnings;
- Vite production build.

An automated browser suite is still deferred. Manual browser validation in the main-registration phase covered category/product/customer/supplier creation, linked product edit, inactivation/reactivation, search, combined filter and pagination through the running Vite/API/SQL Server path. Direct SQL queries confirmed the persisted normalized rows.
