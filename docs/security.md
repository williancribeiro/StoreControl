# Security boundaries

StoreControl is a local study laboratory, not a production deployment template. The stable baseline still treats security behavior as real application behavior, but the supplied Compose file intentionally optimizes for a reproducible single-machine environment.

## Tracked and local configuration

- no working password or signing key belongs in Git, README examples or `.env.example`;
- `scripts/Initialize-LocalEnvironment.ps1` creates independent random values in the ignored `.env` and does not print them;
- demonstration users are opt-in and receive generated passwords;
- Compose rejects missing SQL Server, Redis, RabbitMQ and JWT secrets before startup;
- the Web container never receives backend credentials;
- test-only identities and passwords inside the integration-test process are fixtures for disposable databases, not deployable defaults.

Any value that has ever appeared in Git must be treated as public permanently. Earlier sample literals were demonstrative rather than real secrets, so history was not rewritten. If one of those values was reused in a local service, rotate it or recreate only the disposable StoreControl environment before relying on the hardened configuration.

## Local network boundary

Every published Compose port binds to `127.0.0.1`. Separate edge and backend networks prevent the Web container from directly contacting SQL Server, Redis or RabbitMQ. Redis requires authentication, RabbitMQ and SQL Server use generated passwords, and application containers prohibit privilege escalation.

These controls do not make the Compose file internet-ready. Port binds must not be changed to all interfaces as a deployment shortcut.

## HTTP and identity controls

- controllers and policies remain the authorization boundary; React guards only shape user experience;
- access tokens are short-lived and kept in memory;
- refresh tokens are rotating, stored only as hashes and sent through an HttpOnly SameSite cookie;
- account lockout and per-source login/refresh rate limiting work together;
- authentication responses are non-cacheable and failed login logs omit the supplied identifier;
- public health responses contain only aggregate status; detailed SQL smoke information requires authentication;
- API responses carry defensive browser headers, and the Docker Web server adds a same-origin content security policy;
- Swagger remains available only in the Development environment.

Redis caching never stores credentials, access/refresh tokens, permission grants or user profiles. Dashboard store scope is resolved from SQL before every cache lookup, and category/reporting HTTP responses disallow browser/proxy storage. Generation/filter-aware keys isolate returned read models; prefixes must be deployment-specific. Only API receives Redis credentials in Compose. See [caching.md](caching.md) for invalidation and availability behavior.

## Deliberate local compromises

Local Docker uses HTTP and disables `Secure` on the refresh cookie because there is no development certificate shared with the Web container. The local database connection uses `sa` and trusts the container certificate. These decisions are acceptable only with loopback-only ports and disposable development data.

Before any real deployment, at minimum:

- terminate HTTPS at a trusted reverse proxy and keep refresh cookies `Secure`;
- use an external secret store and a rotation procedure;
- provision separate least-privilege database identities for migrations and runtime;
- configure known forwarded-header proxies and distributed/gateway rate limiting;
- set deployment-specific allowed hosts and origins;
- protect management and health endpoints at the network layer;
- define backup, audit retention, dependency scanning and incident-response procedures;
- review CSP and request-size limits for the deployed frontend and future upload/import features.

Those production concerns remain outside the current local-only scope and must not be represented as already implemented.
