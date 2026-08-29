# Main registrations

Status: categories, products, customers and suppliers are implemented end to end.

## Boundaries

Catalog owns Category and Product. Parties owns Customer and Supplier. These are master-data modules: Product deliberately has no quantity or store reference. Inventory owns current quantity as a Product + Store + StockBalance relationship with explanatory movements.

The request path is:

    React registration page
    → Axios
    → REST controller + permission policy
    → Application service contract
    → Infrastructure service
    → StoreControlDbContext
    → catalog or parties SQL schema

## Data rules

- codes are required, trimmed, uppercased and unique per entity;
- barcode is optional, contains 8, 12, 13 or 14 digits and is unique when present;
- customer and supplier documents are optional, stored with digits only, accept 11 or 14 digits and are unique within their entity;
- optional e-mails are normalized to lowercase and validated;
- cost price uses decimal(18,4); sale price uses decimal(18,2); values are non-negative and rounded away from zero at their persisted scale;
- create operations start active; edits can activate or inactivate without deleting history;
- all edits require the Base64 rowversion returned by the last read;
- a category must be active for a new product association;
- inactivating a category does not rewrite or inactivate products already linked to it, preserving independent history and avoiding an implicit cascade.

Application checks provide useful conflict messages. Filtered unique indexes, foreign keys, check constraints and rowversion remain the database integrity boundary under races.

## Queries

Every list supports page and pageSize (maximum 100), search, active-state filter and explicit sort fields. Product lists additionally filter by category and search code, barcode, name and description. Unsupported sort fields or directions return validation Problem Details.

No cache is used. The current queries are indexed, inexpensive and require fresh administration data; Redis will only be added after a measured read use case justifies invalidation complexity.

## Permissions

Each resource has read and manage policies. Administrator has every permission. The initial mappings grant:

- Gerente: read/manage for all four registrations;
- Vendedor: category/product read and customer read/manage;
- Estoquista: category/product read, product manage and supplier read.

Supplier management and category management therefore remain Manager/Administrator operations by default. Policies are enforced in API controllers; React only hides unavailable navigation and controls.

## Demonstration data

When Seed__DemoDataEnabled=true, DbMigrator idempotently ensures 8 categories, 120 products, 30 customers and 12 suppliers. Product prices, category links and unique identifiers are deterministic. Production-like environments can disable all business demo rows while retaining security reference definitions.
