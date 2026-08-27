# Project state

Last updated: 2026-08-27.

## Current phase

Main registrations remain complete. The current maintenance increment hardens local secrets, authentication endpoints, diagnostics, containers and CI without adding a business module.

## Implemented

Foundation and security:

- .NET 10 modular monolith with Domain, Application, Infrastructure, API, Worker and DbMigrator;
- separate React/TypeScript/Vite frontend;
- EF Core SQL Server DbContext, migration-only DbMigrator, Swagger, Problem Details, structured logs, correlation ID, initial OpenTelemetry and health endpoints;
- ASP.NET Core Identity, four fixed profiles, store assignments, JWT, hashed rotating refresh tokens and backend permission policies;
- user, role/permission and store administration;
- generated ignored local secrets, opt-in demo identities, authenticated smoke diagnostics and minimal anonymous health payloads;
- per-source login/refresh rate limiting, non-cacheable authentication responses, sanitized correlation IDs and defensive HTTP headers;
- loopback-only Compose ports, authenticated Redis, segmented Docker networks and no-new-privileges application containers;
- CI-generated ephemeral SQL credential and commit-pinned official GitHub actions.

Main registrations:

- Category, Product, Customer and Supplier domain entities with normalization and invariants;
- no stock quantity on Product; inventory remains a future Product + Store + Stock module;
- REST list/create/update APIs with server pagination, search, active filters and explicit sorting;
- product category filtering and code/barcode/name/description search;
- unique category/product/customer/supplier codes;
- filtered unique product barcode and normalized party document indexes;
- decimal(18,4) cost and decimal(18,2) sale prices with non-negative SQL checks and deterministic rounding;
- category-product restrictive FK; inactive categories reject new associations without rewriting existing products;
- active/inactive lifecycle without deletion;
- rowversion concurrency on every main-registration edit;
- React pages for all four resources, React Hook Form writes, filters, sort, pagination and permission-aware actions;
- versioned registration permissions for Administrador, Gerente, Vendedor and Estoquista;
- idempotent demonstration master-data seed;
- focused unit and real-SQL integration tests;
- architecture, database, API, frontend, authentication, testing, debugging and master-data documentation.

## Pending modules

Inventory, purchasing, sales, sale cancellation, cash, transfers, reporting, Redis cache integration, RabbitMQ producer/consumer integration and business Worker workloads.

MFA, external identity providers, password-recovery e-mail and self-registration remain outside the current phase.

## Migrations

- 20260827124717_InitialFoundation: platform.FoundationRecords and foundation-version row;
- 20260827133444_AuthenticationAndStores: Identity, permissions, stores, store-scoped roles, refresh tokens, constraints, indexes and rowversion;
- 20260827210014_MainRegistrations: catalog and parties schemas, categories, products, customers, suppliers, price checks, FK, filtered unique indexes, query indexes and rowversion.

All three migrations execute from an empty SQL Server database in integration tests. MainRegistrations is applied to the local StoreControl database. An idempotent SQL script containing all migrations was generated successfully.

## Seed data

Reference seed always ensures:

- 4 system roles;
- 14 permission definitions and Administrator mappings;
- 3 stores: CENTRAL, NORTE and SUL;
- a version marker for the one-time default registration grants on existing operational roles.

When Seed__DemoDataEnabled=true it also ensures:

- 4 demo users, one for each profile;
- 8 categories;
- 120 products distributed coherently among those categories;
- 30 customers;
- 12 suppliers.

The seed was executed twice after this phase without duplicates or overwrites. The local manual validation added one category, product, customer and supplier, so local totals are currently 9, 121, 31 and 13 respectively.

## Authorization defaults

- Administrador: all 14 permissions;
- Gerente: read/manage all four main registrations;
- Vendedor: categories/products read and customers read/manage;
- Estoquista: categories/products read, products manage and suppliers read.

Adding these policies was the only security-module adaptation made by the registration phase. The current hardening increment adds rate limiting and non-cacheable auth responses without changing JWT or refresh-token semantics. The versioned seed adds the new grants once to existing roles, preserves later custom mappings and rotates security stamps of affected existing users.

## Tests

29 tests pass:

- 16 unit tests for architecture boundaries, Store, Category, Product and party invariants, barcode/document validation, money rounding and active state;
- 13 integration tests against real SQL Server for migration metadata/from-zero execution, Identity/JWT/refresh/security behavior, authentication rate limiting, diagnostic authorization/minimal disclosure, safe correlation IDs, store concurrency, category/product lifecycle and linking, decimal persistence, duplicate conflicts, product rowversion, category integrity, seed search/filter/sort/pagination and party authorization/normalization.

Integration tests create uniquely prefixed temporary databases and verify the prefix before deletion. GitHub Actions starts an ephemeral SQL Server Developer container for this suite.

## Required services

- SQL Server is required for DbMigrator, API readiness, authentication, registration persistence and integration tests;
- Redis and RabbitMQ remain provisioned but unused by code;
- Worker still has no business background workload;
- frontend development expects API on localhost:5090 through the Vite proxy.

## Important decisions

- master data is company-wide in this phase; future store-bound stock and transactions must validate store assignments;
- Product intentionally has no quantity;
- resources are inactivated rather than deleted;
- application duplicate checks improve messages, while SQL unique indexes remain authoritative under concurrency;
- all main-registration edits use optimistic rowversion concurrency;
- cost permits four decimal places; sale price permits two; Domain rounding uses AwayFromZero and SQL precision matches;
- no cache was added because these administration queries are indexed, inexpensive and require fresh reads;
- no stored procedure or view was introduced without a concrete query benefit;
- API never migrates or seeds;
- tracked examples contain no working local secrets; `scripts/Initialize-LocalEnvironment.ps1` creates the ignored `.env`, while `scripts/Set-LocalEnvironment.ps1` prepares host-development variables without printing them;
- Compose is explicitly local-only and is not a production deployment template; see [security.md](security.md).

See [master-data.md](master-data.md), [authentication.md](authentication.md) and [architecture.md](architecture.md).

## Commands

Canonical commands and local demo setup are in the root README.md. Backend integration tests require STORECONTROL_TEST_DB_CONNECTION with create/drop database rights.

## Known limitations

- there is no inventory, purchase, sale or cash behavior yet;
- customer/supplier documents validate normalized length, not Brazilian CPF/CNPJ check digits;
- codes are unique within each resource, not globally across every resource;
- frontend has compiler/lint/build gates and completed manual browser validation, but no committed component or browser E2E suite yet;
- Redis, RabbitMQ, Worker business paths, metrics export and a bundled observability UI remain pending;
- role creation/deletion remains unsupported; the four system profiles are fixed;
- the supplied Compose still uses local HTTP and SQL `sa`; real deployment requires TLS, external secret management and least-privilege runtime/migration identities;
- any old public sample value remains permanently public in Git history and must never be reused.

## Validation status

Validated on Windows on 2026-08-27 with .NET SDK 10.0.400, Node 24.19.0, npm 11.17.0 and local SQL Server Express:

- dotnet restore StoreControl.sln: passed;
- dotnet format StoreControl.sln --verify-no-changes --no-restore: passed after normalizing EF-generated line endings;
- dotnet build StoreControl.sln --no-restore: passed with zero warnings and zero errors;
- dotnet test StoreControl.sln --no-build against `Server=.\SQLEXPRESS` with integrated authentication: 29 passed, 0 failed;
- migration discovery and execution from an empty database: passed;
- MainRegistrations applied to the local StoreControl database: passed;
- idempotent SQL migration script generated: passed, 21,883 bytes;
- second DbMigrator run: passed with stable row counts and one versioned permission marker;
- npm ci: passed with 0 reported vulnerabilities;
- npm run typecheck: passed;
- npm run lint: passed with zero warnings;
- npm run build: passed, 107 modules transformed;
- npm audit --omit=dev: passed with 0 reported vulnerabilities;
- dotnet list package --vulnerable --include-transitive: no vulnerable packages reported;
- local environment generator and loader: passed without emitting secret values;
- generated `.env` Compose interpolation/syntax: passed;
- empty `.env.example`: rejected by Compose as intended;
- current tracked-tree scan: no former working demo/CI values, private-key markers or common token patterns found;
- live Vite/API pair: passed;
- administrator login: passed with 14 emitted permissions;
- manual browser category create: passed (MAN-CAT-0827);
- manual browser product create/link/edit/inactivate/reactivate: passed (MAN-PRD-0827);
- manual browser customer and supplier create: passed (MAN-CLI-0827 and MAN-FOR-0827);
- manual product search: passed with one matching row;
- manual active/category filter and pagination: passed with 15 rows split into pages of 10 and 5;
- frontend console errors/warnings during validation: none;
- direct SQL verification: normalized customer/supplier documents, product/category FK, edited values, prices and active state persisted correctly;
- GitHub Actions workflow now generates and masks its SQL password at runtime instead of tracking one.

Docker image build, Nginx runtime configuration and container startup could not be executed. The Docker Desktop Linux engine pipe remains absent and `docker info` cannot connect to `npipe:////./pipe/dockerDesktopLinuxEngine`. This is the same host-level blocker recorded in the earlier phases; repairing or resetting Docker Desktop is outside repository scope. Compose interpolation and syntax pass with a generated environment, while image build and service health must be rerun after the daemon is available.
