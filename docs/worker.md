# Worker

The Worker process is executable and shares the application and infrastructure composition. In the foundation it logs once that no business background workload is registered and then waits for shutdown.

Outbox publishing and RabbitMQ consumers remain pending. The Worker does not emit fake events or simulate completed work.
