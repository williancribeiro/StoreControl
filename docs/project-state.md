# Project state

Last updated: 2026-08-30.

## Current phase

Prompt 7 is complete: sale cancellation and the full store-transfer flow are implemented across backend, frontend, SQL, tests and documentation.

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
- no stock quantity on Product; the inventory module now owns Product + Store positions and movements;
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

Inventory:

- Product + Store stock aggregate with composite-key StockBalance and no quantity field on Product;
- decimal(18,3) current/minimum quantities, deterministic Domain rounding and low-stock calculation;
- immutable signed StockMovement ledger with before/after quantities, reason, actor, correlation and optional business reference;
- initial, purchase, sale, adjustment, transfer, reversal and other entry/exit movement types, while the public write API currently exposes only manual adjustments;
- position/history APIs with server pagination, search, filters, sorting and enforced per-store scope;
- manual adjustment and minimum-stock APIs protected by inventory policies and Problem Details errors;
- explicit ReadCommitted adjustment transaction executed through EF's SQL retry strategy;
- optimistic concurrency with rowversion, 409 conflicts and unique-key protection for simultaneous first creation;
- SQL constraints for non-negative balance, movement direction, nonzero changes and before/change/after arithmetic;
- React position, movement-history and adjustment screens with permission-aware navigation and actions;
- idempotent coherent position/initial-movement seed for every active product/store pair;
- Domain and real-SQL tests for money/quantity precision, rollback, authorization, store scope, constraints and concurrent exits;
- inventory, architecture, database, API, testing and project-state documentation.

Transfers:

- persisted Created, InTransit, Received and Cancelled lifecycle with append-only status history;
- creation without stock effect, transactional origin dispatch and transactional destination receipt;
- paired `TransferOut` / `TransferIn` ledger movements with separate idempotency keys;
- transfer-root update lock plus StockBalance rowversion for concurrent last-unit protection;
- SQL lifecycle, distinct-store, quantity and reference constraints;
- backend permission enforcement requiring access to both stores;
- REST list/details/create/dispatch/receive/cancel APIs with pagination, search and filters;
- React history, new-transfer and detail/action screens with lazy routes and retained idempotency keys;
- Domain and real-SQL tests for lifecycle, no-effect creation, pairing, replay, cancellation, concurrency and scope.

Purchasing:

- PurchaseOrder aggregate with historical product code/name, ordered/received quantities, decimal unit cost/line totals, receipts and append-only status history;
- Created, Pending, PartiallyReceived, Received, Finalized and Cancelled lifecycle with explicit valid transitions;
- creation that writes no stock, submit, partial/full receipt, finalization and cancellation only before receipt;
- purchase list/details REST APIs with pagination, search, store/supplier/status/date filters and sorting;
- purchases.read/purchases.manage policies plus server-side store scope for every query and write;
- explicit ReadCommitted receipt transaction through EF retry strategy, SQL Server UPDLOCK on the order root and optimistic rowversion;
- receipt, order/item updates, balance entries, positive PurchaseReceipt movements and status history committed atomically;
- required client UUID idempotency key, replay response and filtered unique stock-reference protection;
- React new-purchase, history and detail/receipt pages with permission-aware actions and retained idempotency key on uncertain retry;
- idempotent demonstration seed with created/pending orders but no artificial receipt or stock effect;
- Domain and real-SQL tests for lifecycle, creation without stock, receipt, replay, rollback, invalid status, authorization and store scope;
- purchase-flow, architecture, database, API, authentication, frontend, testing and debugging documentation.

Sales and cash:

- finalized Sale aggregate with immutable product code/name/current-price snapshots, decimal quantities, discount, subtotal and total;
- exact 1–10 payment split supporting Cash, debit/credit card, Pix, voucher and other;
- optional active customer, authenticated operator, store and open cash-session relationships;
- one ReadCommitted EF/SQL transaction for sale/items/payments, every stock balance/exit movement and every cash payment movement;
- cash-session `UPDLOCK` serializing sale against another sale/close, plus StockBalance rowversion across different registers;
- sale UUID idempotency with safe exact replay and mismatched-payload conflict;
- CashRegister, rowversion-protected open/closed CashSession and append-only signed CashMovement ledger;
- idempotent opening/manual movement, physical expected cash, non-negative withdrawal rule, closing declaration and difference;
- filtered unique open-session index, sale/payment/movement references, restrictive FKs and SQL financial/lifecycle checks;
- sales.read/sales.manage/cash.read/cash.manage policies with server-side store scope;
- paginated/filterable sales and cash history/detail REST APIs;
- React PDV cart, product search, optional customer, quantity/discount calculation, multiple payments and transactional feedback;
- React cash opening, live ledger, entry, exit, closing, history and session detail;
- Domain and real-SQL tests for calculation, multiple products/payments, insufficient stock, last-item concurrency, rollback, cash lifecycle, permissions and scope;
- cancellation without deletion, rowversion/idempotency, append-only status audit, stock return and signed payment refunds in one transaction;
- sales-flow, cash-flow, architecture, ADR, database, API, authentication, frontend, inventory, testing and debugging documentation.

## Pending modules

Reporting, Redis cache integration, RabbitMQ producer/consumer integration and business Worker workloads.

MFA, external identity providers, password-recovery e-mail and self-registration remain outside the current phase.

## Migrations

- 20260827124717_InitialFoundation: platform.FoundationRecords and foundation-version row;
- 20260827133444_AuthenticationAndStores: Identity, permissions, stores, store-scoped roles, refresh tokens, constraints, indexes and rowversion;
- 20260827210014_MainRegistrations: catalog and parties schemas, categories, products, customers, suppliers, price checks, FK, filtered unique indexes, query indexes and rowversion.
- 20260828140319_Inventory: inventory schema, product/store balances, movement ledger, rowversion, check constraints, restrictive FKs and query/history indexes.
- 20260830144940_PurchasesAndReceiving: purchasing aggregate, receipt/history tables, rowversion, checks, indexes and idempotent inventory-reference uniqueness.
- 20260830190812_SalesAndCash: sales/cash schemas, finalized sale/item/payment rows, registers/sessions/signed ledger, rowversion, checks, restrictive relationships and idempotency/concurrency indexes.
- 20260830233557_SaleCancellation: sale cancellation metadata/history/rowversion, refund-session references and signed one-to-one payment reversals.
- 20260830234649_StockTransfers: transfer header/items/status history, lifecycle checks, rowversion, restrictive FKs and operation indexes.

All eight migrations execute from an empty SQL Server database in integration tests. EF reports no pending model changes.

## Seed data

Reference seed always ensures:

- 4 system roles;
- 22 permission definitions and Administrator mappings;
- 3 stores: CENTRAL, NORTE and SUL;
- one active CX-01 reference cash register per store;
- version markers for one-time default module grants on existing operational roles.

When Seed__DemoDataEnabled=true it also ensures:

- 4 demo users, one for each profile;
- 8 categories;
- 120 products distributed coherently among those categories;
- 30 customers;
- 12 suppliers;
- one coherent StockBalance and InitialBalance movement per active product/store pair;
- 8 coherent purchase orders: 2 Created and 6 Pending, with no receipts or stock effects.
- no artificial sale, payment or cash-session history.

The reference seed was applied after this phase and direct SQL confirmed exactly three unique active store/register pairs with no artificial session or sale. Existing registration, stock and purchase data remains preserved.

## Authorization defaults

- Administrador: all 22 permissions and global store scope;
- Gerente: read/manage all four main registrations, inventory, purchasing, sales and cash within assigned stores;
- Vendedor: categories/products and inventory read, customers read/manage, plus sales/cash read/manage within assigned stores;
- Estoquista: categories/products read, products manage, suppliers read plus inventory and purchasing read/manage within assigned stores; no sales/cash permission.

This phase adds only the four sales/cash policies; JWT and refresh-token semantics are unchanged. The versioned seed grants them once to existing Gerente and Vendedor roles, preserves later custom mappings and rotates security stamps of affected existing users.

## Tests

80 tests pass:

- 45 unit tests for architecture boundaries, Store, Category, Product, party, stock, transfer, purchase, sale and cash invariants, validation, money/quantity rounding and lifecycle rules;
- 35 integration tests against real SQL Server for migrations/from-zero execution, Identity/JWT/refresh/security behavior, registrations, inventory/transfers, purchase, sale/cancellation and cash atomicity/idempotency/authorization/store scope/SQL constraints.

Integration tests create uniquely prefixed temporary databases and verify the prefix before deletion. GitHub Actions starts an ephemeral SQL Server Developer container for this suite.

## Required services

- SQL Server is required for DbMigrator, API readiness, authentication, registration/inventory/transfer/purchase/sale/cash persistence and integration tests;
- Redis and RabbitMQ remain provisioned but unused by code;
- Worker still has no business background workload;
- frontend development expects API on localhost:5090 through the Vite proxy.

## Important decisions

- master data remains company-wide; inventory and purchasing validate store assignments on every store-bound query and write;
- Product intentionally has no quantity;
- resources are inactivated rather than deleted;
- application duplicate checks improve messages, while SQL unique indexes remain authoritative under concurrency;
- all main-registration edits use optimistic rowversion concurrency;
- stock position reads include virtual zero rows for new product/store pairs; the aggregate is persisted by its first minimum setting or movement;
- every supported stock quantity write creates its explanatory movement in the same transaction;
- stock balance rowversion is the concurrency boundary; stale writers reload after HTTP 409 rather than silently overwriting;
- purchase creation never changes stock; only a receipt invokes the stock aggregate;
- receipt UUID is the idempotency boundary, while an order-root update lock serializes concurrent receipts and one transaction covers purchase plus stock;
- sale finalization is the persistence boundary: the browser may hold a cart, but SQL receives only a complete finalized sale;
- sale UUID is the idempotency boundary, while a cash-session root update lock serializes sale/close and one transaction covers sale, stock, payments and cash;
- all sale prices/totals are calculated from server-loaded catalog data; frontend totals never establish integrity;
- all payment forms enter the cash ledger, while only Cash changes expected physical cash;
- cancelling a sale preserves the original document and adds status history, paired stock returns and signed cash/payment refunds in the current open store session;
- transfers require access to both stores; creation has no stock effect, dispatch removes origin stock and receipt adds destination stock in separate audited transactions;
- cost permits four decimal places; sale price permits two; Domain rounding uses AwayFromZero and SQL precision matches;
- no cache was added because these administration queries are indexed, inexpensive and require fresh reads;
- no stored procedure or view was introduced without a concrete query benefit;
- API never migrates or seeds;
- tracked examples contain no working local secrets; `scripts/Initialize-LocalEnvironment.ps1` creates the ignored `.env`, while `scripts/Set-LocalEnvironment.ps1` prepares host-development variables without printing them;
- Compose is explicitly local-only and is not a production deployment template; see [security.md](security.md).

See [sales-flow.md](sales-flow.md), [cash-flow.md](cash-flow.md), [transfer-flow.md](transfer-flow.md), [purchase-flow.md](purchase-flow.md), [inventory-flow.md](inventory-flow.md), [authentication.md](authentication.md) and [architecture.md](architecture.md).

## Commands

Canonical commands and local demo setup are in the root README.md. Backend integration tests require STORECONTROL_TEST_DB_CONNECTION with create/drop database rights.

## Known limitations

- customer/supplier documents validate normalized length, not Brazilian CPF/CNPJ check digits;
- codes are unique within each resource, not globally across every resource;
- frontend has compiler/lint/build gates, but no committed component or browser E2E suite exists yet; purchase/sale/cash integration is covered at the HTTP/SQL boundary, supplemented by documented manual browser checks when the local runtime is available;
- Redis, RabbitMQ, Worker business paths, metrics export and a bundled observability UI remain pending;
- role creation/deletion remains unsupported; the four system profiles are fixed;
- the supplied Compose still uses local HTTP and SQL `sa`; real deployment requires TLS, external secret management and least-privilege runtime/migration identities;
- any old public sample value remains permanently public in Git history and must never be reused.

## Validation status

Validated on Windows on 2026-08-30 with .NET 10, Node/npm and local SQL Server Express:

- dotnet restore StoreControl.sln: passed;
- dotnet format StoreControl.sln --verify-no-changes --no-restore: passed after normalizing EF-generated line endings;
- dotnet build StoreControl.sln --no-restore: passed with zero warnings and zero errors;
- dotnet test StoreControl.sln --no-build against `Server=.\SQLEXPRESS` with integrated authentication: 80 passed, 0 failed (45 unit and 35 integration);
- migration discovery and execution from an empty database through StockTransfers: passed in every integration-test fixture;
- `dotnet ef migrations has-pending-model-changes`: passed with no model drift;
- idempotent migration SQL script generation: passed;
- SaleCancellation and StockTransfers migrations applied to the local StoreControl database by DbMigrator: passed;
- DbMigrator rerun: passed without duplicate reference registers; direct SQL reports 22 permissions, 3 CX-01 registers for 3 distinct stores and no artificial sale/session rows;
- direct SQL role mappings: Administrador 22, Gerente 19, Vendedor 10 and Estoquista 9 effective default permission rows;
- real-SQL sale tests: normal multi-item/multi-payment commit, exact idempotent replay, insufficient-stock rollback, simultaneous last-item contention, forced cross-module rollback, permission and store scope all passed;
- real-SQL cash tests: idempotent open, signed entry/exit, physical expected balance, close/difference and complete ledger passed;
- real-SQL cancellation tests: status/history preservation, exact replay, stock restoration, signed refund and cross-module rollback passed;
- real-SQL transfer tests: no-effect creation, dispatch/receipt pairing, replay, pre-dispatch cancellation, both-store authorization and last-unit contention passed;
- all existing authentication, registration, inventory and purchasing suites remained green;
- npm ci: passed with 0 reported vulnerabilities;
- npm run typecheck: passed;
- npm run lint: passed with zero warnings;
- npm run build: passed, 132 modules transformed with transfer routes split into independent chunks and no size warning;
- npm audit --omit=dev: passed with 0 reported vulnerabilities;
- dotnet list package --vulnerable --include-transitive: no vulnerable packages reported;
- generated `.env` Compose interpolation/syntax: passed;
- direct SQL on the local database confirmed both new migration IDs and all three transfer tables;
- a live Vite/API/SQL Express environment rendered the real login route with no browser console warning/error; authenticated browser submission was not performed, while authenticated transfer/cancellation behavior was exercised by the HTTP/SQL integration suite.

The authoritative end-to-end behavior validation is the ASP.NET Core HTTP integration suite against freshly migrated SQL Server databases, not mocks. It authenticates real seeded users and verifies durable rows/rollback across sales, cancellations, payments, transfers, stock and cash.

Docker image build, Nginx runtime configuration and container startup could not be executed. The Docker Desktop Linux engine pipe remains absent and `docker info` cannot connect to `npipe:////./pipe/dockerDesktopLinuxEngine`. This is the same host-level blocker recorded in the earlier phases; repairing or resetting Docker Desktop is outside repository scope. Compose interpolation and syntax pass with a generated environment, while image build and service health must be rerun after the daemon is available.
