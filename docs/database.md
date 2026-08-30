# Database

SQL Server is the transactional authority. Migrations live in backend/src/StoreControl.Infrastructure/Persistence/Migrations and are applied only by StoreControl.DbMigrator.

## Schemas and tables

platform:

- FoundationRecords: technical smoke-test metadata.

organization:

- Stores: code, name, active state, timestamps and rowversion. Code is unique.

catalog:

- Categories: unique code, name, optional description, active state, timestamps and rowversion;
- Products: unique code, filtered-unique optional barcode, category FK, descriptive fields, decimal(18,4) cost, decimal(18,2) sale price, active state, timestamps and rowversion.

parties:

- Customers and Suppliers: unique code, filtered-unique normalized optional document, optional contact data, active state, timestamps and rowversion.

inventory:

- StockBalances: composite product/store key, decimal(18,3) current and minimum quantities, timestamps and rowversion;
- StockMovements: signed decimal(18,3) change, before/after balances, movement type, reason, responsible user, UTC occurrence, correlation and optional business reference.
- StockTransfers: origin/destination, Created/InTransit/Received/Cancelled lifecycle, actor/operation metadata and rowversion;
- StockTransferItems: historical product snapshots and decimal(18,3) quantities;
- StockTransferStatusHistory: append-only lifecycle audit.

purchasing:

- PurchaseOrders: store/supplier/creator FKs, lifecycle status, dates, notes, total and rowversion;
- PurchaseOrderItems: immutable product code/name snapshot, ordered/received decimal(18,3) quantities, decimal(18,4) unit cost and decimal(18,2) line total;
- PurchaseReceipts: client-generated UUID primary key, order/actor, timestamp and notes;
- PurchaseReceiptItems: receipt lines linked to the exact order item and product;
- PurchaseStatusHistory: append-only status, actor, timestamp and description.

sales:

- Sales: unique operation UUID, store/customer/user/cash-session FKs, finalized/cancelled lifecycle, cancellation audit/session, rowversion and decimal(18,2) subtotal/discount/total;
- SaleItems: immutable product code/name, decimal(18,3) quantity and historical decimal(18,2) price/subtotal;
- SalePayments: 1–10 positive decimal(18,2) payment splits and optional provider/reference text.
- SaleStatusHistory: append-only finalized/cancelled audit with actor, time and description.

cash:

- CashRegisters: active named terminals with unique code per store and rowversion;
- CashSessions: idempotent opening UUID, opener/closer, status, physical expected/declared/difference amounts and rowversion;
- CashMovements: signed Opening/SalePayment/CashIn/CashOut/SaleRefund ledger, actor, correlation, reference, optional manual operation UUID and distinct original/reversed sale-payment links.

security:

- Users, Roles, UserRoles, UserClaims, RoleClaims, UserLogins and UserTokens from ASP.NET Core Identity;
- Permissions and RolePermissions;
- UserStoreRoles with a unique user/store index;
- RefreshTokens with unique token hash, family index, expiry index, self-reference to replacement and rowversion.

Normalized e-mail and normalized username are protected by filtered unique indexes. Password hashes and Identity security stamps are stored by Identity. Plain refresh tokens are never persisted.

## Migrations

- 20260827124717_InitialFoundation: creates platform.FoundationRecords and the version row;
- 20260827133444_AuthenticationAndStores: creates security and organization schemas, Identity tables, permissions, stores, store-scoped roles, refresh sessions, indexes, FKs and rowversion columns.
- 20260827210014_MainRegistrations: creates catalog and parties schemas, the four master-data tables, price checks, foreign keys, filtered unique indexes, query indexes and rowversion columns.
- 20260828140319_Inventory: creates inventory.StockBalances and StockMovements with composite relationship, rowversion, ledger constraints, restrictive foreign keys and history/query indexes.
- 20260830144940_PurchasesAndReceiving: creates the purchasing schema, purchase aggregate/receipt/history tables, constraints, rowversion and receipt-reference uniqueness in the inventory ledger.
- 20260830190812_SalesAndCash: creates sales/cash schemas, finalized sale/item/payment tables, register/session/signed-ledger tables, financial/lifecycle checks, restrictive relationships, rowversion and idempotency/concurrency indexes.
- 20260830233557_SaleCancellation: adds sale rowversion/cancellation audit, status history, refund-session linkage and one-to-one signed payment reversals; existing finalized sales receive a history row.
- 20260830234649_StockTransfers: adds transfer header/items/status history, lifecycle checks, restrictive relationships, rowversion and idempotency/query indexes.

All migrations were validated from an empty database and in a generated idempotent SQL script.

## Seeds

DbMigrator seeds system roles, 22 permission definitions, default mappings, CENTRAL/NORTE/SUL and one active CX-01 cash register per store idempotently. With the explicit demo switch it also ensures demo users plus 8 categories, 120 products, 30 customers, 12 suppliers, a coherent initial position/movement for every active product/store pair and 8 purchase orders (2 created, 6 pending). It does not seed artificial sales or cash sessions. Re-running does not duplicate data, reset passwords, overwrite existing master records, repeat initial movements, recreate purchase orders or duplicate registers.

## Concurrency and constraints

Store, main-registration, purchase, sale, stock-balance and cash-session updates supply Base64 rowversion; stale updates return HTTP 409. Stock adjustments use an explicit ReadCommitted transaction inside the SQL retry execution strategy so the balance update and ledger insert commit together. Purchase receipt uses the same transaction strategy and locks the purchase root. Sale finalization locks the cash-session root and commits the sale, every balance/stock movement, payment and cash movement together; StockBalance rowversion protects contention across different registers. Cancellation locks sale and refund-session roots and commits status history, stock returns and payment refunds together. Cash manual/close operations use that same root lock. Refresh rotation also uses rowversion so two requests cannot successfully rotate the same token.

Application validation improves error messages, but SQL unique indexes, foreign keys and check constraints remain the final integrity boundary. Inventory constraints reject negative balances, zero changes, invalid movement direction, inconsistent before/change/after arithmetic and incomplete transfer lifecycle metadata. Purchasing constraints protect status ranges, quantity bounds, received-at-most-ordered, line totals and required snapshots. Sales constraints protect finalized/cancelled lifecycle, cancellation metadata, financial equations, historical data and payment validity. Cash constraints protect lifecycle, signed direction, physical balances and original/refunded sale-payment linkage. Receipt/sale/cancellation/transfer UUIDs plus filtered business-reference indexes prevent committed stock effects from being applied twice.

## Configuration

The connection key is ConnectionStrings:StoreControl. It must come from environment variables, local secrets or Compose. EF design-time commands also accept STORECONTROL_DB_CONNECTION.

Future reporting and integration schemas will be introduced only with their modules. Views and stored procedures remain deferred until a query has a concrete benefit; current inventory, purchase, sales and cash queries remain understandable indexed EF Core SQL. Purchase receiving, sale finalization and cash mutation deliberately use parameterized SQL for SQL Server `UPDLOCK` root queries because EF LINQ has no equivalent lock hint.
