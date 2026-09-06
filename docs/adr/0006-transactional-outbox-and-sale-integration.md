# ADR 0006: transactional Outbox and asynchronous sale integration

Status: accepted for Prompt 10; implementation in progress.

## Selected event

The first integration event is `sale.finalized.v1`. A finalized sale may trigger a digital receipt or an external accounting/ERP handoff, but neither operation establishes sale, stock, payment or cash integrity. The Worker will simulate the receipt delivery as a durable, inspectable integration result. It will not pretend to contact an external provider.

The event carries identifiers and historical sale values, but no e-mail address, token or credential. The Worker resolves the current customer destination from SQL and stores only its SHA-256 hash in the delivery audit. A sale without an e-mail is recorded as skipped without destination rather than treated as a processing failure.

No cancellation event, report refresh or cache invalidation is added merely to demonstrate messaging. Existing synchronous transactions remain authoritative.

## Delivery guarantees

Sale finalization writes one Outbox row in the same SQL transaction as sale, items, payments, stock and cash. The sale operation UUID is also the event/message UUID, making idempotent sale replay produce no second Outbox event. RabbitMQ downtime cannot roll back or block a committed sale.

An API hosted dispatcher claims unpublished Outbox rows with a short SQL lease, publishes persistent messages with publisher confirms and marks a row published only after broker confirmation. If the process fails after broker confirmation and before the SQL mark, it can publish again. The system therefore provides at-least-once delivery rather than claiming exactly-once delivery.

The Worker uses manual acknowledgements. It commits the consumer Inbox and simulated delivery in one SQL transaction before `ack`. A duplicate with the same consumer/message ID and payload hash is acknowledged without another effect. Reusing that identity for different content is a permanent error.

## RabbitMQ topology

- durable topic exchange `storecontrol.events` with routing key `sale.finalized.v1`;
- durable main queue `storecontrol.worker.sale-finalized`, one consumer prefetch at a time;
- durable direct retry exchange and three durable retry queues with increasing TTLs; expired messages dead-letter back to the topic exchange;
- durable direct dead-letter exchange and durable queue `storecontrol.worker.sale-finalized.dlq`;
- failed transient processing publishes to the next retry queue with an explicit retry-count header and publisher confirmation, then acknowledges the original delivery;
- malformed/unsupported messages and exhausted retries are rejected without requeue and dead-lettered by the main queue;
- if retry publishing is not confirmed, the original message is negatively acknowledged with requeue so it is not silently lost.

Names and retry delays are environment-configurable and prefixed for isolated tests. Production defaults are 5 seconds, 30 seconds and 2 minutes. Changing queue arguments requires a deliberate topology migration or new queue names because RabbitMQ rejects incompatible redeclarations.

## Lifecycle, availability and observation

API and Worker connections recover automatically. Both hosted loops honor cancellation, cancel consumers and dispose channels/connections. The dispatcher applies SQL backoff after publish failures. The consumer logs message ID, sale ID, attempt and outcome, never payload or destination. RabbitMQ activities propagate W3C trace context through headers; counters cover publish, delivery, retry, dead-letter and duplicate outcomes.

RabbitMQ is optional for synchronous API availability. SQL Outbox backlog is the durable indication of delayed publication. Worker readiness and broker health are operational concerns, not sale-transaction preconditions.

## Verification

Tests must cover atomic sale/Outbox persistence and rollback, sale replay, confirmed publication, consumer effect, duplicate delivery, retry progression, final DLQ, malformed content, broker unavailability, recovery and cooperative shutdown. SQL Server and RabbitMQ integrations use real services with unique topology prefixes; no suite silently replaces the broker with an in-memory mock.

References: [RabbitMQ .NET client guide](https://www.rabbitmq.com/client-libraries/dotnet-api-guide), [publisher confirms](https://www.rabbitmq.com/tutorials/tutorial-seven-dotnet), [consumer acknowledgements](https://www.rabbitmq.com/docs/confirms).
