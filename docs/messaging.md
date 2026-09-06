# Messaging

## Scope and ownership

RabbitMQ carries `sale.finalized.v1`, a post-sale integration event. Its current consumer records a simulated digital-receipt delivery for study and audit: it deliberately does not claim to contact an e-mail or ERP provider. The sale, payments, stock exit and cash entries remain inside the SQL transaction and never depend on RabbitMQ availability.

The event contains only identifiers, historical total and finalization time. It does not contain customer e-mail, credentials or tokens. The Worker reads the current customer destination from SQL Server and stores only its SHA-256 hash. A missing destination is a successful `SkippedNoDestination` outcome.

## End-to-end flow

    SalesController
    → SaleService transaction
      → sale, payment, stock and cash rows
      → integration.OutboxMessages
    → commit HTTP operation
    → API OutboxDispatcher claims SQL row
    → RabbitMQ topic exchange (publisher confirm)
    → main sale-finalized queue
    → Worker SaleFinalizedConsumer (manual ack)
    → SaleFinalizedProcessor SQL transaction
      → integration.SaleReceiptDeliveries
      → integration.InboxMessages
    → commit
    → ack

The sale operation UUID is the Outbox/event/message UUID. A successful HTTP idempotency replay finds the existing sale and creates no second event.

## Producer and Outbox

`SaleService` writes the Outbox row, correlation ID and current W3C `traceparent`/`tracestate` before committing its existing cross-module transaction. `OutboxDispatcher`, hosted only by the API, atomically claims a bounded batch using SQL Server `UPDLOCK`, `READPAST`, `ROWLOCK` and a lease. A crashed dispatcher leaves rows recoverable after the lease expires.

The publisher declares topology idempotently, uses persistent messages, mandatory routing and publisher confirms. It creates a producer span from the stored Outbox parent and writes the resulting W3C headers to RabbitMQ. Only a confirmed publish sets `PublishedAtUtc`. A connection failure records a bounded error, clears the lease and schedules exponential SQL backoff. A crash after confirm but before the SQL update can publish twice; this is expected at-least-once delivery, not an exactly-once claim.

## Topology

Names use `Messaging:TopologyPrefix`; the defaults are:

| Role | Name/type |
| --- | --- |
| event exchange | `storecontrol.events`, durable topic |
| main binding | `sale.finalized.v1` |
| main queue | `storecontrol.worker.sale-finalized`, durable |
| retry exchange | `storecontrol.retry`, durable direct |
| retry queues | main queue + `.retry.1..3`, durable, TTL 5 s / 30 s / 120 s |
| dead-letter exchange | `storecontrol.dead-letter`, durable direct |
| final DLQ | `storecontrol.worker.sale-finalized.dlq`, durable |

Each retry queue dead-letters its expired message back to the event exchange. The main queue dead-letters rejected messages to the final DLQ. Queue arguments are part of RabbitMQ topology identity; changing TTL/DLX on an existing name requires an explicit topology migration or a new prefix/name.

## Consumer, retry and acknowledgement

The Worker sets bounded prefetch and processes callbacks sequentially. For each delivery it restores a remote W3C parent, starts a consumer span, validates envelope type, UUID, schema version and payload against the authoritative sale, and then opens a serializable SQL transaction:

1. read the Inbox key `(consumer, messageId)`;
2. if it exists with the same payload SHA-256, return `Duplicate`;
3. if the identity exists with different content, reject permanently;
4. write the delivery audit and Inbox row;
5. commit, then acknowledge manually.

JSON/envelope/schema/identity errors are permanent and are rejected without requeue. Other failures are considered transient: the Worker publishes to the next retry queue with an incremented `x-retry-count`, waits for publisher confirmation, and only then acknowledges the original. If retry publication fails, it negatively acknowledges and requeues the original. After the configured retries, it rejects the delivery to the DLQ.

The Inbox protects database effects from duplicate delivery. A future real external provider would also require an idempotency key accepted by that provider; the local Inbox cannot make an arbitrary remote side effect transactional.

## Configuration and availability

`Messaging:Enabled=false` is the safe bare-process default. Compose and `scripts/Set-LocalEnvironment.ps1` enable it and supply host, port, virtual host, topology prefix and generated local credentials without printing them. Retry delays, polling, batch, lease, connection timeout, recovery interval and prefetch are bounded by startup validation.

RabbitMQ is not an API readiness dependency. `/health/messaging` reports broker health separately and becomes Degraded during an outage; sales continue to commit to SQL Outbox. Operationally, both broker health and the age/count of unpublished Outbox rows must be monitored.

## Diagnostic sequence

For a missing post-sale result:

1. locate `integration.OutboxMessages` by the sale `OperationId`;
2. inspect `PublishedAtUtc`, `PublishAttempts`, `NextAttemptAtUtc`, lease and `LastError`;
3. inspect the event exchange binding and main/retry/DLQ depths in RabbitMQ Management;
4. correlate API/Worker JSON logs by message ID and correlation ID;
5. inspect `integration.InboxMessages` and `integration.SaleReceiptDeliveries`;
6. inspect `StoreControl.Messaging` publish/delivery counters, latency histograms and producer/consumer spans; the original HTTP `X-Trace-ID` remains the same across the asynchronous path.

Never manually mark an Outbox row published or move a DLQ message before understanding and correcting the failure. See [ADR 0006](adr/0006-transactional-outbox-and-sale-integration.md).
