**English** · [Português](README.pt-BR.md)

# FIAP Games — Orders API

The `Order` aggregate, the purchase lifecycle (`Pending → Paid | Failed`, one-way), the library (a projection over `Paid` orders), and the per-order audit log. Owns the `orders` Postgres schema, including MassTransit's transactional outbox tables.

## Run standalone

```bash
cp .env.example .env
docker compose up --build
```

Brings up this service plus its own Postgres and RabbitMQ. API on `localhost:8084`, Swagger at `/swagger`.

## Run as part of the system

Deployed by the [`orchestration`](https://github.com/tc2-fiap/orchestration) Helm chart alongside the other four backend services and the frontend — see [`../orchestration/README.en-US.md`](../orchestration/README.en-US.md). Reached through the shared Ingress at `/api/orders/*` and `/api/library`.

## What's here

- `Domain/Order.cs` — `Price` is a **snapshot**, read synchronously from [`catalog-api`](https://github.com/tc2-fiap/catalog-api) at creation time and never re-fetched; `Status` only ever moves forward.
- `Domain/OrderEvent.cs` — the per-order audit log (`OrderPlacedEvent`/`PaymentProcessedEvent` payloads, admin-only via `GET /api/orders/{id}/events`).
- Publishes `OrderPlacedEvent` in the same transaction as the order write (EF Core outbox); consumes `PaymentProcessedEvent`, idempotent on `OrderId` — a late or duplicate delivery never reverses a settled order.
- `GET /api/orders/admin` — admin-only, every user's orders.
- `GET /api/orders/admin/events` — admin-only, paginated, filterable by `eventType`/`from`/`to`; the same `OrderEvent` table as `/{id}/events` above, but system-wide instead of scoped to one order (`../documentation/spec/notes.md` 43).
- `POST /api/orders` accepts only a `GameId` — the client can never supply a price. Rejects with `409` if the caller already has an active (`Pending` or `Paid`) order for that game — checked before the `catalog-api` price read, since it's local data; a prior `Failed` order never blocks a retry (`../documentation/spec/notes.md` 42).

## Test

```bash
cd tests/FiapGames.Orders.Tests && dotnet test
```

## Documentation

Full architecture, event contracts, and the project-wide decision record live in [`../documentation/`](../documentation/) (also published at [github.com/tc2-fiap/documentation](https://github.com/tc2-fiap/documentation)) — see [`DOCUMENTATION.en-US.md`](../documentation/narrative/DOCUMENTATION.en-US.md) and [`instructions.md`](../documentation/spec/instructions.md) §4.3.
