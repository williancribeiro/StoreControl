# API

The API uses controllers, JSON and REST-oriented routes. Expected errors use Problem Details with errorCode, traceId and correlationId.

## Authentication

- POST /api/auth/login: verifies e-mail/password, creates session and sets refresh cookie;
- POST /api/auth/refresh: rotates refresh token and returns a new access token;
- POST /api/auth/logout: revokes the current refresh token and clears the cookie;
- GET /api/auth/me: returns current roles, permissions and stores.

Invalid login and refresh return 401 without revealing whether an account exists. Missing/invalid bearer tokens return 401; failed policies return 403.

## Administration

Stores:

- GET /api/stores, policy stores.read;
- POST /api/stores, policy stores.manage;
- PUT /api/stores/{storeId}, policy stores.manage and rowversion concurrency.

Users:

- GET /api/users?page=1&pageSize=20&search=, policy users.read;
- POST /api/users, policy users.manage;
- PUT /api/users/{userId}, policy users.manage;
- POST /api/users/{userId}/reset-password, policy users.manage.

Roles:

- GET /api/roles, policy roles.read;
- GET /api/roles/permissions, policy roles.read;
- PUT /api/roles/{roleId}/permissions, policy roles.manage.

Managers with users.read see only users sharing one of their stores. Initial manage permissions belong only to Administrator.

## Main registrations

All collection endpoints accept page, pageSize, search, isActive, sortBy and sortDirection. Product additionally accepts categoryId. pageSize is limited to 100.

- GET/POST /api/categories and PUT /api/categories/{categoryId}: categories.read/categories.manage;
- GET/POST /api/products and PUT /api/products/{productId}: products.read/products.manage;
- GET/POST /api/customers and PUT /api/customers/{customerId}: customers.read/customers.manage;
- GET/POST /api/suppliers and PUT /api/suppliers/{supplierId}: suppliers.read/suppliers.manage.

POST returns 201 with the created representation. PUT returns the updated representation and requires rowVersion. Duplicate code, barcode or document and stale writes return 409. Invalid money, category, pagination or sort input returns validation Problem Details 400. Resources are inactivated rather than deleted.

## Inventory

- GET /api/inventory/balances: `inventory.read`; page/pageSize, search, storeId, productId, belowMinimum, sortBy and sortDirection;
- GET /api/inventory/balances/{storeId}/{productId}: `inventory.read`; returns a virtual zero position when the persisted aggregate does not exist yet;
- PUT /api/inventory/balances/{storeId}/{productId}/minimum: `inventory.manage`; decimal minimumQuantity and current rowVersion;
- GET /api/inventory/movements: `inventory.read`; page/pageSize, search, storeId, productId, type, fromUtc, toUtc and sorting;
- GET /api/inventory/movements/{movementId}: `inventory.read` and store scope;
- POST /api/inventory/adjustments: `inventory.manage`; storeId, productId, positive/negative direction, decimal quantity, required reason and current rowVersion.
- GET /api/inventory/transfers: `inventory.read`; pagination, search, origin/destination/status/date filters and sorting;
- GET /api/inventory/transfers/{stockTransferId}: `inventory.read`; historical items, actors, lifecycle and rowVersion;
- POST /api/inventory/transfers: `inventory.manage`; UUID `Idempotency-Key`, distinct accessible stores and 1–100 product quantities; no stock change;
- POST /api/inventory/transfers/{stockTransferId}/dispatch: `inventory.manage`; UUID `Idempotency-Key` and current rowVersion; writes origin exits;
- POST /api/inventory/transfers/{stockTransferId}/receive: `inventory.manage`; UUID `Idempotency-Key` and current rowVersion; writes destination entries;
- POST /api/inventory/transfers/{stockTransferId}/cancel: `inventory.manage`; UUID `Idempotency-Key`, current rowVersion and reason; Created only.

Successful adjustment creation returns 201 with both updated position and movement. Insufficient stock, stale rowversion and concurrent aggregate creation return 409. Store scope is always enforced on the server.

## Purchases

- GET /api/purchases: `purchases.read`; page/pageSize, search, storeId, supplierId, status, fromUtc, toUtc and sorting;
- GET /api/purchases/{purchaseOrderId}: `purchases.read`; full items, receipts and status history;
- POST /api/purchases: `purchases.manage`; store, supplier, optional expected date/notes and 1–100 distinct product lines;
- POST /api/purchases/{purchaseOrderId}/submit: `purchases.manage`; current rowVersion;
- POST /api/purchases/{purchaseOrderId}/receipts: `purchases.manage`; current rowVersion, receipt lines, optional notes and required UUID `Idempotency-Key` header;
- POST /api/purchases/{purchaseOrderId}/finalize: `purchases.manage`; current rowVersion;
- POST /api/purchases/{purchaseOrderId}/cancel: `purchases.manage`; current rowVersion and required reason.

Creation returns 201 and never changes stock. A new receipt returns 201 after its purchase/stock transaction commits; replaying the same idempotency key returns 200 with `wasAlreadyProcessed=true`. Invalid lifecycle changes, stale writes, excessive receipt quantities and persistence conflicts return 409. Every list, detail and write is restricted to stores accessible to the authenticated user.

## Foundation

- GET /api/smoke: authenticated real API-to-SQL query with diagnostic metadata;
- GET /health/live: public process liveness with aggregate status only;
- GET /health/ready and /health: public SQL readiness with aggregate status only;
- GET /health/messaging: public optional RabbitMQ health; Degraded still returns HTTP 200 because SQL Outbox preserves sale availability;
- /swagger and /swagger/v1/swagger.json in Development.

Swagger defines bearer authentication. Access tokens are returned in the login/refresh response; refresh tokens are cookie-only.

## Sales

- GET /api/sales: `sales.read`; pagination, search, store/customer/payment/date filters and sorting;
- GET /api/sales/{saleId}: `sales.read`; complete historical item/payment representation;
- POST /api/sales: `sales.manage`; required UUID `Idempotency-Key`, store, open cash session, optional customer, discount, 1–100 item quantities and 1–10 payments.
- POST /api/sales/{saleId}/cancellation: `sales.manage`; required UUID `Idempotency-Key`, current sale rowVersion, reason and open refund cash session from the same store.

The server sources current product prices and calculates every financial value. A new atomic finalization returns 201; exact idempotent replay returns 200 with `wasAlreadyProcessed=true`. Insufficient stock, altered idempotency payload and concurrency conflicts return 409. Invalid products, customer, totals, payment sums or cash session return validation Problem Details. Store scope is enforced on every read and write.

## Cash

- GET /api/cash/registers: `cash.read`, optional storeId;
- GET /api/cash/sessions/current: `cash.read`, required cashRegisterId, returns 204 when closed/not open;
- GET /api/cash/sessions: `cash.read`; pagination, store/register/status/date filters and sorting;
- GET /api/cash/sessions/{cashSessionId}: `cash.read`, complete movement ledger;
- POST /api/cash/sessions: `cash.manage`; opening amount and required UUID `Idempotency-Key`;
- POST /api/cash/sessions/{cashSessionId}/movements: `cash.manage`; CashIn/CashOut, amount, reason, rowVersion and required UUID `Idempotency-Key`;
- POST /api/cash/sessions/{cashSessionId}/close: `cash.manage`; declared physical cash and rowVersion.

Open/manual idempotent replay returns a durable operation result. Duplicate open, stale rowversion, closed-session writes and cash-out beyond expected physical cash return 409.

## Dashboard and reports

All endpoints require `reports.read` and enforce the authenticated store scope. Only dashboard responses use optional Redis; period reports query SQL directly:

- GET /api/dashboard?storeId=&date=: daily finalized sales, revenue, ticket, low stock, top products and seven-day evolution;
- GET /api/reports/sales?fromDate=&toDate=&storeId=: sales totals, daily/store breakdowns and top products;
- GET /api/reports/inventory?page=1&pageSize=20&search=&storeId=&belowMinimum=&sortBy=product&sortDirection=asc: paged persisted stock positions;
- GET /api/reports/movements?fromDate=&toDate=&storeId=: inventory entries/exits grouped by movement type and store;
- GET /api/reports/purchases?fromDate=&toDate=&storeId=: order and receipt amounts grouped by status, store and supplier.

Dates are UTC calendar dates, inclusive at both query boundaries, and period reports accept at most 366 days. Inaccessible stores return 403. Invalid periods, paging or sorting return validation Problem Details 400. Cancelled sales do not contribute to commercial indicators.

Dashboard and category-list JSON contracts are unchanged by caching. Category lists without search can use Redis after permission checks. Reporting and category HTTP responses use `Cache-Control: no-store`; internal Redis caching is described in [caching.md](caching.md). `GET /health/cache` reports optional-cache Healthy/Degraded with HTTP 200, independently from SQL readiness; its anonymous body contains only aggregate status.
