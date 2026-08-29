# API

The API uses controllers, JSON and REST-oriented routes. Expected errors use Problem Details with errorCode, traceId and correlationId.

## Authentication

- POST /api/auth/login: verifies e-mail/password, creates session and sets refresh cookie;
- POST /api/auth/refresh: rotates refresh token and returns a new access token;
- POST /api/auth/logout: revokes the current refresh token and clears the cookie;
- GET /api/auth/me: returns current roles, permissions and stores.

Invalid login and refresh return 401 without revealing whether an account exists. Missing/invalid bearer tokens return 401; failed policies return 403.

## Administration

Stores:

- GET /api/stores, policy stores.read;
- POST /api/stores, policy stores.manage;
- PUT /api/stores/{storeId}, policy stores.manage and rowversion concurrency.

Users:

- GET /api/users?page=1&pageSize=20&search=, policy users.read;
- POST /api/users, policy users.manage;
- PUT /api/users/{userId}, policy users.manage;
- POST /api/users/{userId}/reset-password, policy users.manage.

Roles:

- GET /api/roles, policy roles.read;
- GET /api/roles/permissions, policy roles.read;
- PUT /api/roles/{roleId}/permissions, policy roles.manage.

Managers with users.read see only users sharing one of their stores. Initial manage permissions belong only to Administrator.

## Main registrations

All collection endpoints accept page, pageSize, search, isActive, sortBy and sortDirection. Product additionally accepts categoryId. pageSize is limited to 100.

- GET/POST /api/categories and PUT /api/categories/{categoryId}: categories.read/categories.manage;
- GET/POST /api/products and PUT /api/products/{productId}: products.read/products.manage;
- GET/POST /api/customers and PUT /api/customers/{customerId}: customers.read/customers.manage;
- GET/POST /api/suppliers and PUT /api/suppliers/{supplierId}: suppliers.read/suppliers.manage.

POST returns 201 with the created representation. PUT returns the updated representation and requires rowVersion. Duplicate code, barcode or document and stale writes return 409. Invalid money, category, pagination or sort input returns validation Problem Details 400. Resources are inactivated rather than deleted.

## Inventory

- GET /api/inventory/balances: `inventory.read`; page/pageSize, search, storeId, productId, belowMinimum, sortBy and sortDirection;
- GET /api/inventory/balances/{storeId}/{productId}: `inventory.read`; returns a virtual zero position when the persisted aggregate does not exist yet;
- PUT /api/inventory/balances/{storeId}/{productId}/minimum: `inventory.manage`; decimal minimumQuantity and current rowVersion;
- GET /api/inventory/movements: `inventory.read`; page/pageSize, search, storeId, productId, type, fromUtc, toUtc and sorting;
- GET /api/inventory/movements/{movementId}: `inventory.read` and store scope;
- POST /api/inventory/adjustments: `inventory.manage`; storeId, productId, positive/negative direction, decimal quantity, required reason and current rowVersion.

Successful adjustment creation returns 201 with both updated position and movement. Insufficient stock, stale rowversion and concurrent aggregate creation return 409. Store scope is always enforced on the server.

## Foundation

- GET /api/smoke: authenticated real API-to-SQL query with diagnostic metadata;
- GET /health/live: public process liveness with aggregate status only;
- GET /health/ready and /health: public SQL readiness with aggregate status only;
- /swagger and /swagger/v1/swagger.json in Development.

Swagger defines bearer authentication. Access tokens are returned in the login/refresh response; refresh tokens are cookie-only.
