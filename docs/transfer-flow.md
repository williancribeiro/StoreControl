# Stock transfer flow

Status: backend and frontend implemented.

## Model and lifecycle

A transfer is a persisted aggregate with immutable product code/name snapshots, requested quantities, operation actors/timestamps, SQL Server rowversion and append-only status history:

    Created → InTransit → Received
       ↓
    Cancelled

Creation only records intent and never changes stock. Dispatch is allowed only from `Created` and writes one `TransferOut` movement per item in the origin store. Receipt is allowed only from `InTransit` and writes the matching `TransferIn` movements in the destination store. While status is `InTransit`, quantities are intentionally outside both store positions and can be explained as goods in transit.

Cancellation is allowed only in `Created`, before any stock effect. An in-transit transfer cannot be silently cancelled because doing so would conceal whether the shipment returned to origin or arrived at destination; a future return workflow must model that physical event explicitly.

## Transactions and idempotency

Create, dispatch, receipt and cancellation require their own UUID `Idempotency-Key`. Exact replays return `wasAlreadyProcessed=true`. Unique operation indexes and the stock ledger's unique business-reference index prevent a committed dispatch or receipt from being applied twice.

Dispatch and receipt run in explicit `ReadCommitted` transactions inside EF Core's SQL retry strategy:

    POST transition + current rowversion
    → inventory.manage policy + both-store scope
    → UPDLOCK transfer root
    → validate lifecycle
    → load ordered StockBalance roots
    → apply Domain movements
    → append transfer status history
    → one SaveChanges
    → COMMIT

A stale transfer or StockBalance rowversion returns 409. For two transfers competing for the final unit, one stock update commits and the other operation rolls back without a status or movement side effect. SQL checks also prohibit negative stock and invalid transfer lifecycle metadata.

## Store scope

The backend requires access to both origin and destination on every list, detail and write path. This prevents a user assigned only to the origin from reading or creating a document that exposes another store. The existing `inventory.read` and `inventory.manage` policies protect reads and transitions; no security decision depends on the browser.

## API

- `GET /api/inventory/transfers`: paginated search/filter/sort;
- `GET /api/inventory/transfers/{id}`: items, actors, rowversion and full history;
- `POST /api/inventory/transfers`: create with no stock effect;
- `POST /api/inventory/transfers/{id}/dispatch`: origin exit;
- `POST /api/inventory/transfers/{id}/receive`: destination entry;
- `POST /api/inventory/transfers/{id}/cancel`: cancel a Created transfer.

Movement references use `StockTransferDispatch` or `StockTransferReceipt` plus the transition UUID. For debugging, start at transfer history, follow its operation UUID into `inventory.StockMovements`, and compare each before/change/after quantity with the current balance.

The React area provides paginated history, filters, a product-selection editor and a lifecycle detail page. Each transition keeps its UUID after an uncertain error, displays server Problem Details and replaces its local rowversion only after a confirmed response.
