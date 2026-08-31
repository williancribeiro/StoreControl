# ADR 0004: Audited sale reversals and two-stage store transfers

Status: accepted on 2026-08-30.

## Context

Deleting a sale cannot explain restored stock or refunded money. Moving inventory directly from one store to another also hides the operational period in which merchandise has left the origin but has not yet been accepted at the destination.

## Decision

Sale cancellation preserves original items, prices and payments. It appends status history, `SaleReversal` stock movements and signed `SaleRefund` cash movements in one transaction. Refunds are posted to a currently open session in the sale's store; an original closed session is never reopened or retroactively altered.

A store transfer is its own aggregate. Creation has no stock effect, dispatch changes `Created` to `InTransit` and writes origin exits, and receipt changes `InTransit` to `Received` and writes destination entries. Cancellation is permitted only before dispatch. Each transition has an idempotency UUID, append-only history and rowversion protection.

Both operations remain inside the modular monolith's single SQL Server transaction boundary. Existing sales and inventory permissions are reused, with transfer access requiring scope to both stores.

## Consequences

- historical commercial values and ledger entries are never deleted or rewritten;
- goods in transit are explicit and temporarily absent from both store balances;
- a cash refund can fail when the selected session lacks physical money, rolling the entire cancellation back;
- an in-transit cancellation/return requires a future explicit physical-return workflow rather than a status shortcut;
- root update locks plus rowversion and SQL constraints provide deterministic concurrency without a distributed transaction.
