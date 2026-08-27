# StoreControl specification

## Objective

StoreControl is a study laboratory for building, operating, debugging and maintaining a realistic enterprise store-management system. The first complete baseline must be functional, reproducible locally and free of intentional defects before any later bug-injection exercises.

## Architectural scope

- modular monolith;
- C# and .NET 10;
- ASP.NET Core REST API and separate .NET Worker;
- React, TypeScript and Vite frontend, without Next.js;
- SQL Server with EF Core migrations, explicit SQL, views, procedures, indexes, constraints and concurrency control where they have concrete value;
- Redis for justified cache scenarios;
- RabbitMQ with Outbox, producer, consumer, routing, acknowledgement, retry, DLQ and idempotency;
- Docker Compose for local execution without paid services;
- structured logs, correlation, traces, metrics and health checks;
- xUnit, integration tests and Testcontainers for high-risk persistence behavior;
- automated backend and frontend validation.

## Planned business modules

- users, authentication, roles, claims, permissions and store scope;
- stores and cash registers;
- products, categories, customers and suppliers;
- stock per store, inventory ledger, adjustments and transfers;
- purchases and receipt-driven stock entry;
- transactional sales, historical prices, discounts, split payments and cancellation;
- cash sessions and movements;
- reports, dashboard and selective caching.

## Baseline rules

- stock is never stored as `Product.Quantity`;
- stock and money invariants are enforced by the server and database, never by the frontend alone;
- sale finalization, cancellation and purchase receipt are transactional;
- business documents are preserved rather than physically deleted;
- financial values use `decimal` with explicit precision and rounding;
- RabbitMQ and Redis are not transactional authorities;
- no cloud, microservices, Kubernetes, Kafka, Event Sourcing or broad CQRS framework is required;
- incomplete modules remain explicitly pending rather than appearing functional.

The detailed flows will be maintained in the dedicated documents in this directory as each module is implemented.
