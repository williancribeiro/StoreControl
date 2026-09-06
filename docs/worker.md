# Worker

`StoreControl.Worker` is the RabbitMQ consumer composition root. It shares Application/Infrastructure and SQL Server models with the modular monolith, but does not host HTTP endpoints or publish the transactional Outbox.

Its current responsibility is `sale.finalized.v1`:

- establish an automatically recovering RabbitMQ connection;
- declare the durable topology idempotently;
- consume with bounded prefetch and manual acknowledgement;
- validate event envelope/payload against SQL Server;
- record an Inbox identity and simulated receipt audit atomically;
- recognize safe duplicates;
- route transient failures through confirmed TTL retries;
- route permanent/exhausted messages to the DLQ;
- emit structured outcome logs, spans and metrics without payload or destination data.

The API owns Outbox publication because the Outbox is produced by API business transactions. The Worker owns post-sale consumption. Neither side moves sale, inventory, payment or cash integrity out of SQL.

On shutdown the hosted consumer observes the cancellation token, cancels its broker consumer when possible and disposes its channel/connection. An in-flight unacknowledged delivery is requeued by RabbitMQ. Initial connection failures and later channel shutdowns enter a bounded recovery loop rather than terminating the process silently.

With `Messaging:Enabled=false`, the process starts in intentionally inactive mode. Compose enables messaging and waits for Migrator and RabbitMQ health before starting Worker. Optional `OpenTelemetry:OtlpEndpoint` exports `StoreControl.Messaging` telemetry.

See [messaging.md](messaging.md) for topology, delivery semantics and debugging.
