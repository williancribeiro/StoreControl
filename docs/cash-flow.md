# Cash flow

Status: opening, operation, sale payments, manual entry/exit, close and history are implemented.

## Model

`cash.CashRegisters` defines named terminals by store. The reference seed creates one active `CX-01` per store. `cash.CashSessions` represents one operator shift and has rowversion; a filtered unique index allows at most one Open session per register.

`cash.CashMovements` is an append-only signed ledger:

- Opening: positive physical opening amount, omitted when opening is zero;
- SalePayment: positive amount linked one-to-one to a sale payment;
- CashIn: positive manual reinforcement;
- CashOut: negative manual withdrawal.

Manual rows require a reason, actor, correlation ID and client UUID. A withdrawal cannot make expected physical cash negative. Committed movement rows are never edited or deleted through the API.

## Open

`POST /api/cash/sessions` requires `cash.manage` and a UUID `Idempotency-Key`. It validates store scope and active register/store, then creates the Open session plus optional Opening movement atomically. The operation UUID is unique; identical replay returns 200, while a second open session for the register returns 409.

## Sale payment

Sale payments are not posted through a standalone cash endpoint. `SaleService` adds them while it owns the sale transaction. Each `SalePayment` has exactly one `CashMovement`, enforced by a filtered unique FK index. Card, Pix, voucher and other methods remain in the ledger for reconciliation; only Cash changes `ExpectedCashAmount`.

## Manual entry and exit

`POST /api/cash/sessions/{id}/movements` accepts only CashIn or CashOut, current Base64 rowversion, positive absolute amount, reason and `Idempotency-Key`. The Domain converts CashOut to a negative signed amount. An update lock plus rowversion protects the session, and session/movement commit in one transaction.

Replaying the same movement UUID and normalized data is safe. Reusing it with different data returns 409. Stale UI state also returns 409 and must be reloaded rather than overwritten.

## Close

`POST /api/cash/sessions/{id}/close` requires the current rowversion and non-negative physical amount counted by the operator. In one transaction it records:

    DeclaredClosingAmount = counted cash
    DifferenceAmount = counted cash - expected cash
    Status = Closed
    ClosedBy / ClosedAt

The same update lock used by sale finalization prevents a close from committing midway through a sale on that session. Closed sessions reject sales and new manual movements. Close is not idempotently repeated because the first response contains a durable closed representation; a retry receives a lifecycle conflict and the client should read the session.

## Queries and UI

- `GET /api/cash/registers` lists accessible register/store pairs;
- `GET /api/cash/sessions/current?cashRegisterId=` returns 204 when no session is open;
- `GET /api/cash/sessions` supports pagination, store/register/status/date filters and sorting;
- `GET /api/cash/sessions/{id}` returns the full ledger;
- `/cash/current` exposes opening, manual movement, live ledger and closing;
- `/cash/history` and `/cash/sessions/:id` expose audit history and differences.

`cash.read` and `cash.manage` are granted by default to Administrador, Gerente and Vendedor. Estoquista has neither. Every backend path enforces store scope independently of frontend navigation.

## Reconciliation

For a session, validate physical cash using Opening + CashIn - CashOut + SalePayment where payment method is Cash. This signed sum must equal `ExpectedCashAmount`. All payment methods can be reconciled against sale payment rows using `CashMovements.SalePaymentId`; `ReferenceType=Sale` and the sale operation UUID provide a human-debuggable cross-module path.
