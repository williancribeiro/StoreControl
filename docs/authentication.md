# Authentication

Status: implemented for identity, administration, registrations and store-scoped inventory.

## Model

ASP.NET Core Identity owns password hashing, password validation, normalized e-mail lookup, lockout and global role membership. The four profiles are Identity roles:

- Administrador;
- Gerente;
- Vendedor;
- Estoquista.

Perfis are not duplicated in a second profile table. Global Administrator membership uses security.UserRoles. Store-scoped assignments use security.UserStoreRoles, which enforces at most one role for a user in each store. This allows one person to hold different roles in different stores without making every role global.

Permissions are stable codes stored in security.Permissions and linked through security.RolePermissions. The current catalog is:

- stores.read and stores.manage;
- users.read and users.manage;
- roles.read and roles.manage;
- categories.read and categories.manage;
- products.read and products.manage;
- customers.read and customers.manage;
- suppliers.read and suppliers.manage;
- inventory.read and inventory.manage.

The API registers one authorization policy per code. Controllers enforce policies; React uses the same codes only to shape navigation and controls.

## Login and access token

POST /api/auth/login validates the Identity account, active flag, lockout and password. Five failed attempts lock the user for 15 minutes. Successful login updates LastLoginAtUtc, persists a refresh session and returns:

- a signed HS256 JWT valid for 15 minutes;
- expiry time;
- current user, effective roles, permissions and active store assignments.

The signing key must be supplied through Authentication__Jwt__SigningKey and must contain at least 32 characters. No signing secret is tracked.

JWT claims include subject, e-mail, display name, JWT ID, Identity security stamp, roles, permission codes and store assignments. JwtBearer validates issuer, audience, signature and lifetime. On every authenticated request, the API also verifies that the user is active and that the token security stamp still matches SQL Server.

Changing a user, resetting a password or changing role permissions rotates affected security stamps. Existing access tokens then fail immediately rather than remaining usable for their nominal 15-minute lifetime.

Login and refresh are additionally limited to 10 requests per source address in a 60-second fixed window. Rejections use HTTP 429 and Problem Details. This application limit complements account lockout; an internet deployment still needs equivalent controls at its trusted reverse proxy or gateway. Failed logins do not record a user-supplied e-mail address in application logs, and authentication responses are marked `Cache-Control: no-store`.

## Refresh token

The browser receives the refresh token only as .StoreControl.RefreshToken:

- HttpOnly;
- SameSite=Strict;
- path limited to /api/auth;
- Secure in normal configuration and explicitly disabled only for documented local HTTP development.

The frontend keeps the access token in memory and never writes either token to localStorage or sessionStorage.

SQL Server stores only a SHA-256 hash of each cryptographically random refresh token. A successful POST /api/auth/refresh:

1. finds the hash;
2. verifies expiry, revocation and active user;
3. revokes the presented token;
4. creates a replacement in the same family;
5. saves both changes atomically with rowversion concurrency;
6. returns a new access token and replaces the cookie.

Concurrent reuse is rejected. Reuse of an already rotated token is treated as replay and revokes every still-active token in that family. Logout revokes the current refresh token and deletes the cookie. Password reset revokes all refresh sessions for the affected user.

## Store scope

Administrator is global and can list all stores. Other users receive only active stores explicitly assigned through UserStoreRoles. Current administration writes require manage permissions; only Administrator receives those permissions in the initial seed.

Current master data is company-wide and uses resource read/manage policies. Gerente initially manages all four registrations; Vendedor reads catalog and manages customers; Estoquista reads catalog/suppliers and manages products. Inventory is store-bound: Gerente and Estoquista receive read/manage, Vendedor receives read, and every backend inventory query/write validates the requested store against the authenticated assignment. A hidden frontend menu or a client-supplied store ID is never treated as authorization.

## Frontend lifecycle

AuthProvider attempts one deduplicated refresh when the application starts. Axios attaches the in-memory bearer token and, after a 401, attempts a single shared refresh before retrying the original request. Login, refresh and logout endpoints are excluded from recursive retry.

ProtectedRoute requires authentication. PermissionRoute controls user experience for an area, but is not a security boundary.

## Demonstration seed

The DbMigrator always seeds roles, permissions and three reference stores idempotently. Demonstration users are added only when Seed__DemoDataEnabled=true and both password variables are present. Versioned seeds add registration and inventory grants once to existing Gerente, Vendedor and Estoquista roles, while later runs preserve customized mappings. Affected existing sessions are invalidated through security-stamp rotation.

No working demonstration password is versioned. `scripts/Initialize-LocalEnvironment.ps1 -EnableDemoData` creates random local passwords in the ignored `.env`; README.md lists only the fixed `.local` identities required to select a profile. The example environment file keeps secret fields empty, and Compose refuses to start without required generated values.

## Deliberate limitations

- no external identity provider, MFA, password-recovery e-mail or user self-registration yet;
- e-mail is treated as a confirmed internal identifier in this local laboratory;
- access-token validation currently queries SQL Server for account state on every authenticated request; Redis caching is intentionally deferred until measurements justify it;
- roles are fixed system roles; non-administrator permission mappings can be edited, but arbitrary role creation is outside this phase.
