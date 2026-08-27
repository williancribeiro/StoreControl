# ADR 0002: JWT sessions and store-scoped roles

Status: accepted on 2026-08-27.

## Context

StoreControl needs realistic browser authentication, backend authorization and users that can hold different operational profiles in different stores. It must run locally without an external identity service.

## Decision

Use ASP.NET Core Identity with GUID users and roles. Treat the four named profiles as roles. Use Identity UserRoles for global Administrator membership and a separate UserStoreRoles relation for one role per user/store.

Issue short-lived signed JWT access tokens. Store access tokens only in browser memory. Use rotating opaque refresh tokens in an HttpOnly SameSite cookie, persist only their hashes, add token-family replay detection and use rowversion for concurrent rotation.

Represent authorization capabilities as permission rows linked to roles. Emit effective permission claims and require backend policies. Validate the Identity security stamp and active account against SQL Server on authenticated requests.

## Consequences

- no paid identity provider is needed;
- store scope is explicit and queryable;
- role, password and account changes invalidate access tokens immediately;
- refresh replay and concurrent reuse can be studied in SQL and API logs;
- authenticated requests perform one account-state SQL query until a measured cache is introduced;
- future store-bound business services must still validate their requested store, rather than relying only on a generic permission claim.
