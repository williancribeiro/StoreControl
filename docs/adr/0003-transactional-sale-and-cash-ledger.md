# ADR 0003: Finalized sale transaction and signed cash ledger

Status: accepted on 2026-08-30.

## Context

A commercial commit must persist the sale, historical item prices, multiple payments, stock exits and cash effects without partial state. Cash close and concurrent last-item sales must also have deterministic boundaries.

## Decision

The public create-sale operation persists an already finalized sale in one SQL Server transaction. It locks the cash-session root, uses StockBalance rowversion for cross-register contention, and writes sale, inventory and cash data through the single StoreControlDbContext. A client UUID is the sale idempotency key.

Cash uses sessions plus an append-only signed movement ledger. All sale payment forms are recorded, while only physical cash changes the session's expected cash amount. Opening and manual movements also accept idempotency UUIDs; closing uses the current rowversion and shares the session lock with sales.

## Consequences

- committed sale rows are complete and immutable in this phase;
- the modular monolith and single database provide a real atomic transaction without distributed coordination;
- one open session per register and restrictive FKs/checks make SQL Server an integrity boundary;
- cancellation must later be a separately designed reversal, not deletion or mutation of historical values;
- a long sale serializes close and other sales for its cash session, an acceptable tradeoff for the current local laboratory and volume.
