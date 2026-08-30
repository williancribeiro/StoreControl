# Purchase flow

Status: implemented for purchase orders, partial/full receipts, finalization and pre-receipt cancellation.

## Aggregate and lifecycle

`PurchaseOrder` is the aggregate root. It owns historical item snapshots, receipts and status history. Each item stores ProductId plus the product code/name seen when the order was created, ordered and received quantities, unit cost with four decimal places and line total with two decimal places.

The supported lifecycle is:

    Created → Pending → PartiallyReceived → Received → Finalized
       │          │
       └──────────┴────────────────────────→ Cancelled

- creation produces `Created` and does not access inventory;
- submit requires at least one item and moves `Created` to `Pending`;
- a partial receipt moves `Pending` or `PartiallyReceived` to `PartiallyReceived`;
- receiving every remaining quantity moves the order to `Received`;
- only a fully received order can be finalized;
- cancellation is allowed only in `Created` or `Pending`, before any receipt, and requires a reason;
- orders, receipts and history are never deleted by the API.

Every status change appends `purchasing.PurchaseStatusHistory` with actor, UTC timestamp and an explanatory description.

## Creation

    React NewPurchasePage
    → POST /api/purchases
    → purchases.manage policy + store-scope check
    → active Supplier/Product validation
    → PurchaseOrder + PurchaseOrderItems + initial status history
    → one EF Core SaveChanges

Product cost is only the frontend default. The submitted decimal unit cost becomes the order's historical value. Product cost changes later do not rewrite an existing purchase.

Creation intentionally inserts no `StockBalance` and no `StockMovement`. This is covered by a real-SQL integration test.

## Receiving and inventory

    POST /api/purchases/{id}/receipts + Idempotency-Key UUID
    → purchases.manage policy + store scope
    → ReadCommitted transaction through EF retry strategy
    → UPDLOCK on the PurchaseOrder row
    → validate current purchase rowversion and every receipt line
    → PurchaseReceipt + items + status history
    → StockBalance.ApplyMovement(PurchaseReceipt) for every product
    → PurchaseOrder update + stock updates + movements in one SaveChanges
    → commit

The root-row update lock serializes receipts for the same order. The purchase rowversion protects explicit stale writers. StockBalance rowversion still protects the underlying inventory aggregate when another business operation changes it concurrently.

Each inventory movement records:

- type `PurchaseReceipt`;
- positive quantity and before/after balances;
- reason containing the order number;
- authenticated actor and request correlation ID;
- `ReferenceType = PurchaseReceipt` and the receipt UUID as `ReferenceId`.

The filtered unique index on `(StoreId, ProductId, ReferenceType, ReferenceId)` is the final duplicate-movement boundary. A failure while writing any order item, receipt, balance or movement rolls back all changes in the transaction.

## Idempotency

Every receipt request requires a client-generated UUID in `Idempotency-Key`. That UUID is the `PurchaseReceipt` primary key. Replaying an already committed key returns HTTP 200 with `wasAlreadyProcessed = true`; the original purchase state is returned and no quantity is applied again. A new receipt returns HTTP 201.

The React details page keeps the key for a failed/uncertain attempt and reuses it when the user retries. A confirmed attempt clears the key before the next receipt.

Idempotency only identifies a retry of the same intended receipt. Clients must not reuse one key for a different order or new business receipt.

## Permissions and store scope

- `purchases.read`: list and details;
- `purchases.manage`: create, submit, receive, finalize and cancel.

Administrador has global access. Gerente and Estoquista initially receive both permissions, limited to their active `UserStoreRoles` assignments. Vendedor receives neither. Controllers enforce policies and `PurchaseService` independently validates the requested order/store; the React menu is not a security boundary.

## Errors and diagnostics

- 400: invalid values, empty/duplicate item list, inactive supplier/product/store or malformed idempotency key;
- 403: missing permission or store outside the authenticated scope;
- 404: purchase/store not found;
- 409: stale rowversion, invalid status, excessive receipt quantity or transactional persistence conflict.

For a failed receipt, search the structured log by correlation ID, then inspect the purchase status/items, the receipt UUID and inventory movements with its reference. A correct rollback leaves no `PurchaseReceipt`, no received quantity and no balance/movement change from that request.
