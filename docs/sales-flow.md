# Sales flow

Status: implemented for finalization and audited cancellation/reversal.

## Boundary

`POST /api/sales` creates an already finalized sale. The client may keep a draft cart, but an incomplete draft is not persisted. Every row in `sales.Sales` therefore represents a completed transaction with items, exact payments, stock exits and cash-ledger entries.

The request must carry a UUID `Idempotency-Key`, an accessible store, an open cash session from the same store, 1–100 distinct products, optional active customer, discount and 1–10 payments. The backend reloads active products and their current sale prices; it never accepts unit prices or calculated totals from the browser.

## Transaction

    React NewSalePage
    → POST /api/sales + bearer token + Idempotency-Key
    → sales.manage policy and store scope
    → SaleService
    → lock cash.CashSessions root with UPDLOCK
    → validate open cash/store/customer/products
    → Sale Domain calculation and historical snapshots
    → StockBalance negative Sale movements
    → CashSession SalePayment movements
    → one SaveChanges
    → SQL COMMIT

The explicit transaction uses `ReadCommitted` inside the EF Core SQL Server execution strategy. The session update lock serializes a sale against another sale or close on the same register. Stock balances retain rowversion protection for concurrent sales through different registers. A failure in any item, payment, stock movement, cash movement or constraint rolls the complete operation back.

## Calculation

- quantities use decimal with three fractional digits;
- catalog sale price and all sale/payment totals use decimal with two fractional digits;
- Domain rounding is `MidpointRounding.AwayFromZero`;
- each line stores product ID plus historical code, name, quantity, unit price and line subtotal;
- subtotal is the sum of rounded line subtotals;
- discount is non-negative and strictly less than subtotal;
- total equals subtotal minus discount;
- every payment is positive and their rounded sum must equal total exactly.

SQL check constraints repeat the status and arithmetic invariants. `double` is not used for money in the backend. The frontend preview is convenience only; server and SQL validation remain authoritative.

## Payments and cash

Supported forms are Cash, DebitCard, CreditCard, Pix, Voucher and Other. A sale can split its exact total across up to ten rows. Each payment creates one linked `cash.CashMovements` row for reconciliation. All payment forms appear in the ledger, but only Cash increases `CashSession.ExpectedCashAmount`, which represents physical money expected during close.

## Idempotency

`sales.Sales.OperationId` is unique. Repeating the same key, user and normalized payload returns HTTP 200 with `wasAlreadyProcessed=true`; a new commit returns HTTP 201. Reusing a committed key with different store, session, customer, discount, item quantities or payments returns 409. The React PDV retains its key after an uncertain/error response and only abandons it after confirmed success.

Inventory also has a unique `(StoreId, ProductId, ReferenceType, ReferenceId)` business reference. Sale exits use `ReferenceType=Sale` and the operation UUID as `ReferenceId`.

## Last-item concurrency

For an on-hand quantity of 1, two requests can both arrive with quantity 1. They cannot both commit:

1. the first request locks the shared cash-session root, applies the stock exit and commits;
2. the second continues with the current balance and receives a 409 insufficient-stock conflict;
3. if separate register roots are involved, StockBalance rowversion detects the conflicting update and returns 409;
4. SQL constraints additionally prohibit a negative persisted balance.

The tested result is one sale, one sale stock movement and zero remaining quantity.

## Read APIs and UI

- `GET /api/sales` supports pagination, search, store/customer/payment filters, date range and explicit sorting;
- `GET /api/sales/{saleId}` returns historical items, payments, user, store, customer and cash register;
- `/sales/new` is the PDV-style cart and settlement screen;
- `/sales` is the paginated history;
- `/sales/:saleId` is the immutable detail view.

`sales.read` and `sales.manage` policies are enforced by the API. Current defaults grant both to Administrador, Gerente and Vendedor; Estoquista has neither. Non-administrators remain limited to assigned stores.

## Cancellation and reversal

`POST /api/sales/{saleId}/cancellation` requires `sales.manage`, the current sale rowversion, an open cash session from the same store, a reason and a UUID `Idempotency-Key`. The finalized sale is never deleted or rewritten as a new sale. One transaction locks the sale and refund-session roots, changes the status to `Cancelled`, appends status history, returns every item through `SaleReversal` stock movements and appends one `SaleRefund` cash-ledger movement for every original payment.

Only a Cash refund reduces expected physical cash; card, Pix, voucher and other refunds remain signed ledger records for reconciliation. A closed original sale session is not reopened or modified: the operator selects the current open session that performs the refund. Insufficient physical cash, stale rowversion, invalid lifecycle, a duplicate reversal or any persistence failure rolls back status, stock and cash together.

`CancellationOperationId` and one-to-one links from each refund to its original `SalePayment` prevent duplicate effects. An exact retry returns `wasAlreadyProcessed=true`; a different cancellation key against an already cancelled sale returns 409. Sale details expose the cancellation actor/time/reason/session, current rowversion and append-only status history.
