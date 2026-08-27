# Database

SQL Server is the transactional authority. Migrations live in backend/src/StoreControl.Infrastructure/Persistence/Migrations and are applied only by StoreControl.DbMigrator.

## Schemas and tables

platform:

- FoundationRecords: technical smoke-test metadata.

organization:

- Stores: code, name, active state, timestamps and rowversion. Code is unique.

catalog:

- Categories: unique code, name, optional description, active state, timestamps and rowversion;
- Products: unique code, filtered-unique optional barcode, category FK, descriptive fields, decimal(18,4) cost, decimal(18,2) sale price, active state, timestamps and rowversion.

parties:

- Customers and Suppliers: unique code, filtered-unique normalized optional document, optional contact data, active state, timestamps and rowversion.

security:

- Users, Roles, UserRoles, UserClaims, RoleClaims, UserLogins and UserTokens from ASP.NET Core Identity;
- Permissions and RolePermissions;
- UserStoreRoles with a unique user/store index;
- RefreshTokens with unique token hash, family index, expiry index, self-reference to replacement and rowversion.

Normalized e-mail and normalized username are protected by filtered unique indexes. Password hashes and Identity security stamps are stored by Identity. Plain refresh tokens are never persisted.

## Migrations

- 20260827124717_InitialFoundation: creates platform.FoundationRecords and the version row;
- 20260827133444_AuthenticationAndStores: creates security and organization schemas, Identity tables, permissions, stores, store-scoped roles, refresh sessions, indexes, FKs and rowversion columns.
- 20260827210014_MainRegistrations: creates catalog and parties schemas, the four master-data tables, price checks, foreign keys, filtered unique indexes, query indexes and rowversion columns.

All migrations were validated from an empty database and in a generated idempotent SQL script.

## Seeds

DbMigrator seeds system roles, permission definitions, default mappings and CENTRAL, NORTE and SUL idempotently. With the explicit demo switch it also ensures demo users plus 8 categories, 120 products, 30 customers and 12 suppliers. Re-running does not duplicate data, reset passwords or overwrite existing master records.

## Concurrency and constraints

Store and main-registration updates supply Base64 rowversion; stale updates return HTTP 409. Refresh rotation also uses rowversion so two requests cannot successfully rotate the same token. EF SaveChanges provides the atomic update/insert transaction for token rotation.

Application validation improves error messages, but SQL unique indexes and foreign keys remain the final integrity boundary.

## Configuration

The connection key is ConnectionStrings:StoreControl. It must come from environment variables, local secrets or Compose. EF design-time commands also accept STORECONTROL_DB_CONNECTION.

Future inventory, purchasing, sales, cash, reporting and integration schemas will be introduced only with their modules. Views and stored procedures remain deferred until a query has a concrete benefit; the current registration queries are ordinary indexed EF Core reads.
