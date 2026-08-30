# Debugging map

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
    → SalesController
    → SaleService store/session/product validation
    → Sale + StockBalance + CashSession Domain rules
    → SQL transaction
    → sales.Sales/SaleItems/SalePayments
    → inventory.StockBalances/StockMovements
    → cash.CashSessions/CashMovements

For a 400, inspect cash-session/store matching, active customer/products, discount and exact payment sum. For a 403, inspect both `sales.manage` and `security.UserStoreRoles`. For a 409, distinguish insufficient stock, rowversion contention and idempotency-payload mismatch using Problem Details plus correlation ID.

To trace a committed sale, start with `sales.Sales.OperationId`. It must match one stock movement reference per distinct product and one cash movement per payment. Reconcile historical item subtotals to the sale subtotal, payment sum to total, and only Cash payment movements to the change in physical `ExpectedCashAmount`. A failed sale must leave no sale row and no new movement under that operation UUID.

## Cash operation

    Browser CashOperationPage
    → cashService Axios + rowVersion / Idempotency-Key
    → cash.read or cash.manage policy
    → CashController
    → CashService locked session
    → CashSession Domain rules
    → cash.CashSessions + append-only CashMovements

For duplicate opening, inspect the filtered open-session index and `CashSessions.OperationId`. For stale entry/exit/close, compare the Base64 rowVersion with the SQL rowversion. Reconcile expected physical cash as Opening + CashIn - CashOut + cash-form SalePayment. Card/Pix/Voucher payments belong in the audit ledger but do not increase physical cash. A close and sale on the same session are serialized on the cash-session row.

## Future asynchronous path

    API transaction
    → integration.OutboxMessages
    → Worker
    → RabbitMQ
    → idempotent consumer
    → SQL Server or Redis

The asynchronous path is architectural guidance only and is not implemented yet.
