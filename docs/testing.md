# Testing

## Unit tests

StoreControl.UnitTests currently verifies:

- Domain references no StoreControl infrastructure project;
- Application does not reference Infrastructure;
- Store normalizes values, changes active state and rejects invalid code;
- Category normalization and active-state changes;
- Product barcode/money validation and deterministic decimal rounding;
- Customer/supplier document, e-mail and active-state rules.

## Integration tests

StoreControl.IntegrationTests boots the real ASP.NET Core API and uses EF migrations against SQL Server. It creates a uniquely named StoreControl_IntegrationTests_* database and verifies the prefix before deleting it.

Coverage currently includes:

- migration discovery and migration from zero;
- successful Identity login;
- signed access token and HttpOnly SameSite refresh cookie;
- protected endpoint returning Problem Details 401;
- Vendedor receiving 403 on an Administrator write;
- Administrator creating a store;
- refresh rotation, replay detection and family revocation;
- user creation with a store role and immediate JWT invalidation after deactivation;
- stale Store rowversion returning 409.
- registration migration discovery and migration from zero;
- category/product create and link, decimal persistence, duplicate conflict, stale rowversion, inactivation/reactivation and active-category invariant;
- product seed search, active/category filters, sort and pagination;
- customer document normalization/duplicate conflict plus Vendedor/Administrador policy differences for customers and suppliers.

Set STORECONTROL_TEST_DB_CONNECTION to a SQL Server connection with create/drop database permission. The connection's database name is replaced with a safe unique test name. GitHub Actions provisions SQL Server Developer as a job service and supplies this variable.

This phase uses a real configured SQL Server rather than Testcontainers so tests can run on the validation host even while its Docker daemon is unavailable. Testcontainers remains appropriate for later suites when the local Docker blocker is repaired.

## Frontend validation

Current automated frontend gates are:

- TypeScript project build;
- Oxlint with zero warnings;
- Vite production build.

An automated browser suite is still deferred. Manual browser validation in the main-registration phase covered category/product/customer/supplier creation, linked product edit, inactivation/reactivation, search, combined filter and pagination through the running Vite/API/SQL Server path. Direct SQL queries confirmed the persisted normalized rows.
