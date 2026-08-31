[English](README.en-US.md) · **Português**

# FIAP Games — Orders API

O agregado `Order`, o ciclo de vida da compra (`Pending → Paid | Failed`, unidirecional), a biblioteca (uma projeção sobre pedidos `Paid`) e o log de auditoria por pedido. Dono do schema `orders` no Postgres, incluindo as tabelas do outbox transacional do MassTransit.

## Rodar de forma independente

```bash
cp .env.example .env
docker compose up --build
```

Sobe este serviço mais seu próprio Postgres e RabbitMQ. API em `localhost:8084`, Swagger em `/swagger`.

## Rodar como parte do sistema

Implantado pelo chart Helm [`orchestration`](https://github.com/tc2-fiap/orchestration) junto com os outros quatro serviços de backend e o frontend — ver [`../orchestration/README.pt-BR.md`](../orchestration/README.pt-BR.md). Acessado pelo Ingress compartilhado em `/api/orders/*` e `/api/library`.

## O que tem aqui

- `Domain/Order.cs` — `Price` é um **snapshot**, lido de forma síncrona do [`catalog-api`](https://github.com/tc2-fiap/catalog-api) no momento da criação e nunca buscado novamente; `Status` só avança, nunca retrocede.
- `Domain/OrderEvent.cs` — o log de auditoria por pedido (payloads de `OrderPlacedEvent`/`PaymentProcessedEvent`, admin-only via `GET /api/orders/{id}/events`).
- Publica `OrderPlacedEvent` na mesma transação da escrita do pedido (outbox do EF Core); consome `PaymentProcessedEvent`, idempotente por `OrderId` — uma entrega tardia ou duplicada nunca reverte um pedido já resolvido.
- `GET /api/orders/admin` — somente admin, pedidos de todos os usuários.
- `GET /api/orders/admin/events` — somente admin, paginado, filtrável por `eventType`/`from`/`to`; a mesma tabela `OrderEvent` do `/{id}/events` acima, mas de todo o sistema em vez de restrita a um pedido (`../documentation/spec/notes.md` 43).
- `POST /api/orders` aceita apenas um `GameId` — o cliente nunca pode fornecer um preço. Retorna `409` se quem chama já tem um pedido ativo (`Pending` ou `Paid`) para aquele jogo — verificado antes da leitura de preço no `catalog-api`, já que é dado local; um `Order` anterior `Failed` nunca bloqueia uma nova tentativa (`../documentation/spec/notes.md` 42).

## Testar

```bash
cd tests/FiapGames.Orders.Tests && dotnet test
```

## Documentação

A arquitetura completa, os contratos de eventos e o registro de decisões do projeto vivem em [`../documentation/`](../documentation/) — ver [`DOCUMENTATION.pt-BR.md`](../documentation/narrative/DOCUMENTATION.pt-BR.md) e [`instructions.md`](../documentation/spec/instructions.md) §4.3 (em inglês).
