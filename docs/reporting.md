# Dashboard and reporting

Status: implemented without cache.

## Scope and authorization

Dashboard and reporting endpoints require `reports.read`. The initial seed grants it to Administrador and Gerente. Administrador can aggregate every store; other users can aggregate only active stores present in `security.UserStoreRoles`. Supplying an inaccessible `storeId` returns 403, and omitting it means all stores accessible to that user rather than all company stores.

The frontend uses the permission to show navigation and controls, but `ReportingController` and `ReportingService` enforce the actual boundary.

## Time and financial semantics

Reporting dates are UTC calendar dates because stores do not yet have individual time-zone configuration. Period endpoints treat `fromDate` and `toDate` as inclusive and accept at most 366 days. Internally, timestamp queries use a half-open interval from 00:00 on the first date through, but not including, 00:00 after the final date.

Only `Finalized` sales contribute to sales count, revenue, ticket, evolution and product ranking. Cancelled sales remain auditable in `sales.Sales`, but are deliberately absent from commercial totals. Gross revenue is the sum of sale subtotals, discount is the document-level discount and revenue is the final total. Product ranking exposes gross item revenue because a sale-level discount is not artificially allocated among items.

Purchase period means order creation date. Order count includes every status so lifecycle activity is visible; the headline, per-store and per-supplier ordered amounts exclude cancelled orders. Received amount is calculated from receipt quantity multiplied by the historical item unit cost for orders selected by that creation period, including receipts registered later.

Stock movement entries and exits are reported as positive magnitudes even though `inventory.StockMovements.QuantityChange` remains signed in the ledger.

## Query choices

| Read model | Implementation | Reason |
| --- | --- | --- |
| daily sales, revenue and evolution | `reporting.vw_DailySales` through keyless EF Core projection | centralizes cancellation filtering and UTC date aggregation reused by dashboard and period report |
| current persisted stock positions | `reporting.vw_StockPosition` through keyless EF Core projection | keeps the product/category/store join and low-stock expression visible in SQL |
| top-selling products | `reporting.usp_TopSellingProducts` through parameterized `DbCommand` | ranking needs grouped item history, a bounded TOP result and an explicit authorized store set |
| movement totals by type/store | LINQ translated by EF Core | grouping and signed conditional sums remain clear and parameterized without handwritten SQL |
| purchase totals by status/store | LINQ translated by EF Core | navigation joins and lifecycle rules are readable and covered by existing relational mappings |

The procedure receives the exact authorized store IDs assembled by the application. They are passed as one parameter and parsed by `STRING_SPLIT`; it never trusts a browser-supplied list. `DbCommand` is used only at this procedure boundary and every value remains a SQL parameter.

Canonical readable SQL is stored in `database/views` and `database/procedures`. EF migrations remain the executable history and contain the same definitions.

## Endpoints

- `GET /api/dashboard?storeId=&date=`: daily sales count, revenue, ticket, low-stock count/items, top five products and seven-day evolution;
- `GET /api/reports/sales?fromDate=&toDate=&storeId=`: period totals, daily evolution, store grouping and top ten products;
- `GET /api/reports/inventory?page=&pageSize=&search=&storeId=&belowMinimum=&sortBy=&sortDirection=`: paged persisted stock positions;
- `GET /api/reports/movements?fromDate=&toDate=&storeId=`: entry/exit totals grouped by type and store;
- `GET /api/reports/purchases?fromDate=&toDate=&storeId=`: order/receipt values grouped by status, store and supplier.

Inventory report pages are limited to 100 rows. A product/store pair is present after its `StockBalance` has been created by seed, minimum configuration or first movement. The operational inventory API remains the source for virtual zero positions that have never been persisted.

## Indexes and query review

`ReportingDashboard` adds:

- `sales.Sales(Status, FinalizedAtUtc DESC, StoreId)` including subtotal, discount and total;
- `inventory.StockMovements(OccurredAtUtc DESC, StoreId, MovementType)` including signed quantity;
- `purchasing.PurchaseOrders(CreatedAtUtc DESC, StoreId, Status)` including total.

`OptimizeReportingQueries` recreates the existing unique `sales.SaleItems(SaleId, ProductId)` index including quantity and line subtotal, so the ranking can read item measures without a separate lookup while preserving duplicate-product protection.

SQL Server `SHOWPLAN_TEXT` on the local schema confirmed an index seek on the new sales reporting index for the period aggregate. The local demonstration database intentionally had no sales during this inspection, so production-like cardinality and duration must be measured again after representative history exists. The 366-day cap, bounded rankings and inventory pagination prevent accidental unbounded UI queries; they do not replace future execution-plan monitoring.

## Cache decision

Redis is not used in this phase. Correctness, authorization and query behavior are established first. A later cache-aside phase may cache dashboard/report responses only after defining store/date/filter-aware keys, TTL, invalidation and SQL fallback.
