# ADR 0001: Modular monolith and single transactional DbContext

Status: accepted.

## Decision

Use a modular monolith with one SQL Server database and one `StoreControlDbContext`. Organize modules by namespaces, folders and SQL schemas rather than separate services or a DbContext per module.

## Rationale

Sales must atomically update sales, inventory, cash and Outbox records. One context and one SQL transaction keep this boundary explicit and debuggable. Separate services or contexts would add coordination complexity without an independent deployment requirement.

## Consequences

Reporting may read across module schemas. Module boundaries must be protected through code organization and tests, not independent databases. Hot invariants may use targeted stored procedures inside the same transaction.
