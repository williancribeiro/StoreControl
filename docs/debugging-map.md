# Debugging map

## Start from evidence

Use one failing request as the diagnostic anchor:

1. In Browser DevTools, reproduce with Network recording enabled and keep the request URL, method, status, timing, `X-Correlation-ID`, `X-Trace-ID` and `Idempotency-Key`. Do not copy Authorization, Cookie, passwords or response data into tickets.
2. Search the Aspire Dashboard trace view by `X-Trace-ID`. The waterfall identifies whether time/failure belongs to ASP.NET Core, `sale.finalize`, Redis, SqlClient, RabbitMQ publication or Worker consumption.
3. Search structured API/Worker logs by `TraceId`, `CorrelationId`, `MessageId` or the durable operation UUID. Compare timestamps; do not infer ordering from interleaved console text alone.
4. Put Visual Studio breakpoints at the named checkpoint in the flow below. Inspect IDs and entity state, but avoid evaluating methods that mutate tracked entities.
5. Query SQL Server using the operation/business reference and reconcile ledgers. Read first; do not "repair" rows until the failed invariant is understood.
6. Use Redis CLI and RabbitMQ Management only when the trace reaches those boundaries. SQL remains the source of truth.

The browser starts the HTTP request but currently does not create a browser OpenTelemetry span. `X-Trace-ID` is therefore the bridge from DevTools into the server trace. See [observability.md](observability.md) for signal names and privacy constraints.

## Tool checkpoints

| Tool | What to verify |
| --- | --- |
| Browser Console | React/runtime error and source-mapped stack; never paste token/cookie values |
| Browser Network | request contract, status, timing, diagnostic headers, Problem Details and whether a retry occurred |
| Visual Studio | controller input, application decision, EF change tracker, transaction boundary and thrown exception |
| Structured logs | durable transition, bounded outcome and IDs; API and Worker service name |
| Trace view | parent/child order, span status and latency across SQL/cache/RabbitMQ |
| SQL Server | authoritative row/status/rowversion, transactionally paired ledgers and Outbox/Inbox state |
| Redis | key existence, generation, TTL and availability; never edit business state because none belongs there |
| RabbitMQ Management | bindings, ready/unacked counts, retry queues, DLQ and consumer connection |

For SQL investigations, begin with parameterized `SELECT` statements in a read-only transaction when practical. For Redis, prefer `TTL`, `GET` for a known diagnostic key and bounded `SCAN`; never use `KEYS` on a shared/large instance. In RabbitMQ, do not purge, acknowledge, requeue or move DLQ messages as a first diagnostic step.

## Diagnostic conventions

- `correlation ID` connects browser responses, Problem Details and structured API logs for one request;
- `Idempotency-Key` identifies a retriable command and prevents a repeated request from duplicating its business effect;
- `rowVersion` is the Base64 representation of SQL Server `rowversion` used to detect stale writes;
- `OperationId` and movement reference fields connect a business command to its inventory or cash ledger entries.

## Login

    Browser LoginPage
    → AuthContext login
    → Axios POST /api/auth/login
    → CorrelationIdMiddleware
    → AuthController
    → AuthenticationService
    → UserManager password/lockout
    → security.Users
    → security.RefreshTokens
    → JwtTokenIssuer
    → JSON access token + Set-Cookie

Checkpoints:

1. Browser Network: status, response Problem Details, Set-Cookie flags and correlation ID.
2. React: LoginPage form errors and AuthProvider session.
3. API logs: rejected e-mail, locked user or successful user ID; never log passwords/tokens.
4. Identity: NormalizedEmail, PasswordHash, IsActive, LockoutEnd and AccessFailedCount.
5. Session: only TokenHash is stored; inspect expiry/revocation/family fields.
6. JWT: issuer, audience, expiry, security-stamp claim and permission claims.

## Protected request

    Axios bearer header
    → JwtBearer signature/lifetime
    → TokenSessionValidator
    → security.Users IsActive + SecurityStamp
    → permission policy
    → controller
    → administration service
    → StoreControlDbContext
    → SQL Server

401 indicates authentication/session validation. 403 indicates an authenticated identity without the required permission. A hidden frontend menu is not evidence that the API authorized an operation.

## Refresh

    AuthProvider or Axios 401 handler
    → POST /api/auth/refresh with HttpOnly cookie
    → SHA-256 lookup
    → rowversion-protected revoke + replacement insert
    → new cookie/access token

For replay diagnosis, follow FamilyId, RevokedAtUtc and ReplacedByTokenId. Reusing a revoked token should revoke the active family and return 401.

## Stores and users

For store update conflicts, compare the Base64 rowVersion from the React form with organization.Stores.RowVersion. HTTP 409 is the expected stale-write outcome.

For unexpected access after an administration change, compare the JWT security-stamp claim with security.Users.SecurityStamp. User and role changes intentionally rotate it.

## Categories, products, customers and suppliers

    Browser registration page
    → registrationService Axios request
    → resource permission policy
    → resource controller
    → ICategory/IProduct/ICustomer/ISupplierService
    → Infrastructure service
    → StoreControlDbContext
    → catalog or parties table

For a 400, inspect the ValidationProblemDetails fields and domain normalization rules. For a 409, distinguish the SQL-backed unique conflict from a stale Base64 rowVersion. For product/category problems, verify catalog.Products.CategoryId, category IsActive and the FK. Product quantity is not present in catalog.Products; debug it through inventory.StockBalances for the exact store and reconcile it against inventory.StockMovements.

## Inventory

    Browser inventory page
    → inventoryService Axios request
    → inventory.read or inventory.manage policy
    → InventoryController
    → InventoryService store-scope check
    → StockBalance Domain rules
    → EF Core transaction
    → inventory.StockBalances + inventory.StockMovements

For an unexpected quantity, filter movements by both StoreId and ProductId and compare their signed sum with QuantityOnHand. For a 409, reload the position and compare rowversion before retrying. For a 403, inspect both the permission claim and security.UserStoreRoles. A failed adjustment should change neither table; use the request correlation ID to find its structured API log.

## Purchase and receipt

    Browser PurchaseDetailsPage
    → purchaseService Axios request + Idempotency-Key
    → purchases.manage policy
    → PurchasesController
    → PurchaseService store-scope/status/rowversion checks
    → PurchaseOrder + StockBalance Domain rules
    → EF Core ReadCommitted transaction
    → purchasing tables + inventory.StockBalances/StockMovements

Creating a purchase should stop at the purchasing tables. If stock changed at creation, trace the movement reference because no legitimate creation path emits one. For receipt problems, correlate `PurchaseReceipts.Id` with `StockMovements.ReferenceId`; each received product should have exactly one positive movement in the order's store. A 409 can mean stale rowversion, invalid lifecycle/quantity or a persistence conflict. A replayed UUID should return 200 and `wasAlreadyProcessed=true` without another movement. A failed transaction should leave no receipt, no item received quantity and no new balance/movement.

## Foundation smoke

    Authenticated browser → SmokePage → Axios → GET /api/smoke → EF Core → platform.FoundationRecords

Public `/health` routes intentionally return only aggregate status. Sign in and use `/system/smoke` when foundation version and SQL Server time are needed for diagnosis.

## Sale finalization

    Browser NewSalePage cart/payments
    → salesService Axios + Idempotency-Key
    → sales.manage policy
    → ASP.NET Core trace / SalesController
    → sale.finalize span / SaleService store/session/product validation
    → Sale + StockBalance + CashSession Domain rules
    → SQL transaction
    → sales.Sales/SaleItems/SalePayments
    → inventory.StockBalances/StockMovements
    → cash.CashSessions/CashMovements
    → integration.OutboxMessages (traceparent + correlation ID)

For a 400, inspect cash-session/store matching, active customer/products, discount and exact payment sum. For a 403, inspect both `sales.manage` and `security.UserStoreRoles`. For a 409, distinguish insufficient stock, rowversion contention and idempotency-payload mismatch using Problem Details plus correlation ID.

To trace a committed sale, start with `X-Trace-ID` for execution timing and `sales.Sales.OperationId` for durable state. It must match one stock movement reference per distinct product, one cash movement per payment and one Outbox row. Reconcile historical item subtotals to the sale subtotal, payment sum to total, and only Cash payment movements to the change in physical `ExpectedCashAmount`. A failed sale must leave no sale row, ledger movement or Outbox event under that operation UUID.

## Sale cancellation

    Browser SaleDetailsPage
    → POST cancellation + rowVersion + Idempotency-Key
    → SaleService locks sale and refund cash session
    → Sale.Cancel + StockBalance SaleReversal + CashSession SaleRefund
    → sales history + inventory ledger + cash ledger in one transaction

Start with `Sales.CancellationOperationId`. The same UUID must appear once per product under `StockMovements.ReferenceType=SaleCancellation` and once per original payment under `CashMovements.ReferenceType=SaleCancellation`. Each refund links to `ReversedSalePaymentId`; only Cash reduces expected physical money. If cancellation returns 409, inspect sale rowversion/status, refund-session lifecycle and expected cash before assuming an inventory error.

## Store transfer

    Browser transfer page
    → transferService Axios + rowVersion / Idempotency-Key
    → inventory policy + both-store scope
    → StockTransfersController
    → StockTransferService locks transfer root
    → StockTransfer + StockBalance Domain rules
    → transfer history + inventory balance/movement transaction

Creation must not have a stock reference. Dispatch uses `StockTransferDispatch` plus `DispatchOperationId` in the origin; receipt uses `StockTransferReceipt` plus `ReceiptOperationId` in the destination. For goods in transit, the origin exit exists while the destination entry does not yet exist. A 409 can indicate stale transfer state, insufficient origin stock or StockBalance contention; all three leave the transition and its movements rolled back.

## Cash operation

    Browser CashOperationPage
    → cashService Axios + rowVersion / Idempotency-Key
    → cash.read or cash.manage policy
    → CashController
    → CashService locked session
    → CashSession Domain rules
    → cash.CashSessions + append-only CashMovements

For duplicate opening, inspect the filtered open-session index and `CashSessions.OperationId`. For stale entry/exit/close, compare the Base64 rowVersion with the SQL rowversion. Reconcile expected physical cash as Opening + CashIn - CashOut + cash-form SalePayment. Card/Pix/Voucher payments belong in the audit ledger but do not increase physical cash. A close and sale on the same session are serialized on the cash-session row.

## Dashboard and reports

    Browser dashboard or ReportsPage
    → reportingService Axios request
    → reports.read policy
    → ReportingController
    → ReportingService store-scope/date validation
    → LINQ / reporting view / parameterized procedure
    → SQL Server

For a missing store, first compare the JWT store claims with `security.UserStoreRoles`; omitting `storeId` still means only accessible stores. For an unexpected sales total, query `reporting.vw_DailySales` and verify that only `Sales.Status=Finalized` rows contribute. Product ranking comes from `reporting.usp_TopSellingProducts`; compare its UTC half-open timestamps and store-ID parameter with the request. Stock reports use persisted rows from `reporting.vw_StockPosition`, while the operational inventory endpoint can also synthesize never-persisted zero positions. Dashboard can use Redis after store-scope validation; period reports still use SQL directly.

## Redis cache

    Browser DashboardPage / category list
    → Axios → JWT/policy → service (dashboard resolves SQL store scope)
    → RedisReadCache → platform.CacheRevisions
    → Redis GET with generation/date/scope/filter key
    → hit: deserialize response
    → miss/fallback: original EF/view/procedure SQL reads
    → successful miss: Redis SET with absolute TTL

For an unexpected cached value, correlate the HTTP request with the `StoreControl.Cache` spans and Debug `CacheRegion`/`CacheOutcome` logs. Compare the normalized request inputs and current generation to the actual key observed with SCAN. Check TTL; a hit must not renew it. Authentication/store-scope and generation SQL queries are expected even on a hit.

For an expected invalidation, inspect the committed generation and the dependency table's `TR_*_CacheRevision` trigger. A sale, receipt, adjustment or transfer changes the dashboard generation through its existing SQL writes. A failed transaction must leave both business data and generation unchanged. Old Redis keys may still exist until expiry but should no longer be selected. Direct imports must preserve trigger execution.

For unavailability, compare `/health/cache` with `/health/ready`: optional Redis can be Degraded while SQL remains Healthy. Redis failures should increase fallback/write-failure counters, log one unavailable transition and keep SQL-backed reads available. A SQL failure must not be converted into a successful stale response. See [caching.md](caching.md) for configuration, metrics and the manual CLI commands.

## Asynchronous post-sale path

    API sale transaction
    → integration.OutboxMessages (stored trace context)
    → API OutboxDispatcher / producer span
    → RabbitMQ events exchange + W3C headers
    → main / retry / DLQ queues
    → Worker consumer span (remote parent)
    → integration.InboxMessages + SaleReceiptDeliveries
    → manual acknowledgement

Start with the HTTP `X-Trace-ID` or sale operation UUID. The trace should cross the stored Outbox parent, API producer and Worker remote consumer; the correlation ID remains a searchable application field. Follow the operation UUID as Outbox/message ID, then inspect `PublishedAtUtc`, attempts/lease/error, queue depth, Worker logs, Inbox and delivery audit. A committed sale with an unpublished Outbox row points to dispatcher/broker availability. A published row without an Inbox result points to routing/consumer/retry/DLQ. An Inbox row proves the database effect committed before acknowledgement. See [messaging.md](messaging.md).
