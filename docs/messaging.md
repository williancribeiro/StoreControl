# Messaging

Status: infrastructure service available, application integration pending.

RabbitMQ is present in Compose but no exchange, queue or message is declared. The first real producer will use a transactional SQL Outbox; consumers will use manual acknowledgement, retry, DLQ and an Inbox for idempotency.
