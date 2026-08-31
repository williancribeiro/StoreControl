# Architecture

## Style

StoreControl is a modular monolith. API, Worker and DbMigrator are separate composition roots around the same application and SQL Server boundary.

    StoreControl.Domain
            ↑
    StoreControl.Application
            ↑
    StoreControl.Infrastructure
            ↑                 ↑
    StoreControl.Api   StoreControl.Worker

    StoreControl.DbMigrator → StoreControl.Infrastructure

## Project responsibilities

- Domain: entities and invariants with no EF Core or ASP.NET Core dependency. It owns Store, Category, Product, Customer, Supplier, stock, purchase, sale and cash aggregates, and stable security names/codes.
- Application: use-case contracts, response models, exceptions and meaningful infrastructure ports.
- Infrastructure: EF Core, SQL Server, Identity entities, session persistence, seeding and administration implementations.
- Api: controllers, JWT issuing and validation, policies, middleware, OpenAPI and Problem Details.
- Worker: future Outbox and consumers; it still has no simulated business workload.
- DbMigrator: the only startup process that applies migrations and reference/demo seeds.
- StoreControl.Web: separate React application using HTTP contracts.

Identity-specific types remain in Infrastructure because they are ASP.NET Core persistence concerns. Domain does not depend on Identity. API implements access-token issuing because it is the HTTP authentication composition root.

## Active request paths

Login:

    Browser LoginPage
    → AuthProvider / Axios
    → POST /api/auth/login
    → AuthController
    → IAuthenticationService
    → ASP.NET Core Identity + StoreControlDbContext
    → SQL Server security schema
    → JWT response + HttpOnly refresh cookie

Protected administration:

    React route and permission-aware controls
    → bearer JWT
    → JwtBearer signature/lifetime validation
    → SQL account/security-stamp validation
    → permission policy
    → Controller
    → Application contract
    → Infrastructure service
    → EF Core
    → SQL Server

Main registration:

    React category/product/customer/supplier page
    → Axios list or write request
    → JWT and resource permission policy
    → REST controller
    → Application module contract
    → Infrastructure module service
    → EF Core query/change tracking
    → catalog or parties SQL schema

Inventory adjustment:

    React stock position or adjustment page
    → Axios with loaded rowversion
    → JWT inventory.manage policy
    → InventoryController
    → IInventoryService
    → InventoryService + Domain stock aggregate
    → explicit SQL transaction
    → inventory.StockBalances update + StockMovements insert

Purchase receipt:

    React purchase details page
    → Axios with purchase rowversion + Idempotency-Key
    → JWT purchases.manage policy
    → PurchasesController
    → IPurchaseService
    → PurchaseService + PurchaseOrder/StockBalance aggregates
    → one explicit SQL transaction with locked purchase root
    → purchasing receipt/history + inventory balance/movements

Sale finalization:

    React PDV cart and payments
    → Axios + Idempotency-Key
    → JWT sales.manage policy
    → SalesController → ISaleService
    → SaleService locks the open cash-session root
    → Sale + StockBalance + CashSession Domain rules
    → one SQL transaction
    → sales rows + inventory exit ledger + cash payment ledger

Cash operation:

    React CashOperationPage
    → cash.read/cash.manage policy
    → CashController → ICashService
    → CashService + CashSession Domain rules
    → rowversion and locked session transaction
    → cash session + signed append-only movement

Store transfer:

    React transfer page
    → inventory.read / inventory.manage policy and both-store scope
    → StockTransfersController → IStockTransferService
    → locked StockTransfer + StockBalance Domain rules
    → one transaction per dispatch or receipt
    → transfer status history + paired inventory ledger references

Foundation smoke:

    Authenticated browser → React SmokePage → GET /api/smoke → EF Core → SQL Server

Frontend guards improve experience. JwtBearer, policies, account-state validation and service invariants are the security boundary.

## Persistence

One StoreControlDbContext derives from IdentityDbContext and covers the transactional model. SQL schemas and module folders provide boundaries while preserving future transactions across sales, inventory, cash and Outbox. The API never migrates automatically.

Current schemas:

- platform: foundation metadata;
- security: Identity, permissions, store assignments and refresh sessions;
- organization: stores.
- catalog: categories and products;
- parties: customers and suppliers.
- inventory: stock balances by product/store and their immutable movement ledger.
- purchasing: purchase orders, historical items, receipts, receipt items and status history.
- sales: finalized sales, historical items and exact payment splits;
- cash: store registers, open/closed sessions and signed movements.

## Cross-cutting behavior

- X-Correlation-ID is returned and enriched into structured logs;
- malformed correlation IDs are replaced before reaching response headers and structured logs;
- expected validation, not-found, conflict and forbidden exceptions become appropriate Problem Details;
- unexpected exceptions are logged once at the API boundary;
- OpenTelemetry traces ASP.NET Core and outgoing HTTP;
- public liveness/readiness responses expose only aggregate status; the authenticated smoke endpoint carries diagnostic database metadata;
- login and refresh use a fixed-window per-source rate limit in addition to Identity account lockout;
- API and Web add defensive response headers; the Web container also applies a same-origin content security policy;
- CancellationToken flows through controllers and EF operations where cancellation is meaningful.

## Decisions

- [ADR 0001](adr/0001-modular-monolith-and-single-dbcontext.md): modular monolith and one DbContext;
- [ADR 0002](adr/0002-jwt-refresh-and-store-scoped-roles.md): JWT, refresh rotation and store-scoped roles.
- [ADR 0003](adr/0003-transactional-sale-and-cash-ledger.md): transactional finalized sales and signed cash ledger.
- [ADR 0004](adr/0004-audited-reversals-and-two-stage-transfers.md): audited sale reversals and two-stage store transfers.

Redis and RabbitMQ remain provisioned but unused until a concrete cache or asynchronous workflow exists.
Local security boundaries and production gaps are documented in [security.md](security.md).
