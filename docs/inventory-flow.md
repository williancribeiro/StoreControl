# Inventory flow

Status: implemented for stock positions, manual adjustments and movement history.

## Model

Inventory is a relationship, not a property of Product:

    Product + Store → StockBalance
                    → StockMovement history

`inventory.StockBalances` has the composite key `(StoreId, ProductId)`. It keeps only the current quantity, minimum quantity, timestamps and SQL Server `rowversion`. Quantities use `decimal(18,3)`; fractional units are supported and Domain rounding uses `AwayFromZero`.

`inventory.StockMovements` is the explanatory ledger. Every row records the signed change, quantity before, quantity after, type, reason, responsible user, UTC timestamp, correlation ID and optional business reference. Movement rows are not exposed for update or deletion.

A product/store pair with no persisted balance is read as a zero position with no rowversion. This makes newly created products immediately visible in position queries without manufacturing a stock movement. The first minimum update or first movement creates the persisted aggregate. Initial demonstration balances are persisted by the idempotent seed.

## Invariants

- quantity on hand and minimum quantity cannot be negative;
- a movement change cannot be zero;
- `QuantityAfter = QuantityBefore + QuantityChange`;
- entry movement types must have a positive change and exit types a negative change;
- a movement must have a non-blank reason and responsible user;
- a negative operation cannot make the current balance negative;
- application writes update the balance and insert its movement in the same transaction;
- clients must provide the rowversion they read before changing an existing balance.

The Domain owns the direction of every movement type. The public adjustment API accepts only `PositiveAdjustment` and `NegativeAdjustment`; purchase, sale, transfer and reversal types are reserved for their future business flows so callers cannot disguise those operations as manual adjustments.

SQL check constraints are the final guard for non-negative balances, the movement equation, type direction and non-blank reasons. Foreign keys prevent removal of a product, store, balance or responsible user that is referenced by history. Direct positive SQL updates could bypass the ledger and are therefore outside the supported write path; application database identities must not be granted ad-hoc write access in a real deployment.

## Adjustment transaction

    React adjustment form
    → POST /api/inventory/adjustments
    → inventory.manage policy
    → validate active accessible store and product
    → begin ReadCommitted transaction inside EF retry strategy
    → load or create StockBalance
    → apply expected rowversion
    → Domain calculates before/change/after
    → update StockBalance + insert StockMovement
    → commit

Insufficient stock, validation failures, stale rowversion and concurrent first creation do not leave a movement or partial balance update. Expected concurrency conflicts return HTTP 409 and the client must reload before deciding whether to retry.

For the classic `stock = 1` race, both requests may read the same rowversion. SQL Server accepts the first conditional update and changes its rowversion. The second update affects zero rows, EF raises `DbUpdateConcurrencyException`, the transaction rolls back and the API returns 409. The integration suite verifies that exactly one movement exists afterward.

## Store scope and permissions

- `inventory.read`: positions and movement history;
- `inventory.manage`: minimum stock and manual adjustments.

Administrator has global store access. Other users are restricted by `security.UserStoreRoles` in the service query, independently of frontend controls. The initial mappings grant read/manage to Gerente and Estoquista, read to Vendedor, and all permissions to Administrador.

## API and frontend

Position filters include store, product, low-stock state, search, pagination and explicit sorting. Movement filters include store, product, movement type, UTC period, search, pagination and sorting. See `docs/api.md` for routes.

The React inventory area contains:

- Posição atual: quantity/minimum status, store and product filters, minimum edit and adjustment shortcut;
- Movimentações: signed ledger with before/after values, reason, actor, reference and correlation;
- Ajustes: searchable product selection, store, direction, quantity, required reason, loaded concurrency version and confirmation details.

Frontend permission guards control navigation and actions. JWT policies and store-scope checks in the backend are the security boundary.

## Demonstration data and future modules

With demonstration data enabled, DbMigrator creates one initial balance and one `InitialBalance` movement for every active seeded product/store pair. Values are deterministic, include low-stock examples and are idempotent.

Purchasing, sales and transfers must call the same aggregate behavior within their own transactions. They must provide a business reference and use the corresponding movement type; they must not update `QuantityOnHand` separately or call the manual adjustment HTTP endpoint internally.

## Debugging path

For a wrong balance, start with the position response, then query movements for the exact product/store and sum `QuantityChange`. Use the movement correlation ID to find the API structured log and trace. For a 409, compare the submitted Base64 rowversion with the current position. For a 403, inspect both the permission claim and the user's store assignment.

    Browser → Inventory page → Axios → InventoryController
    → IInventoryService → InventoryService → EF Core transaction
    → inventory.StockBalances + inventory.StockMovements
