# ADR 0005: NATS Consumers and Cross-Module Data Ownership

## Status

Accepted.

## Context

The skeleton already publishes integration events through module-owned outboxes and a NATS JetStream adapter. Consumers need the same modular discipline: infrastructure can run the transport loop, but modules should own idempotency, projection writes, and business decisions.

Cross-module reads are tempting in a modular monolith because everything is deployable together. The project direction is stricter: modules may reference another module's `.Contracts` project only. They must not use another module's EF model, DbContext, domain model, repository, or application service.

## Decision

Add a generic NATS JetStream consumer loop in `Gma.Framework.Messaging.Nats` and consumer contracts in `Gma.Framework.Messaging`.

Modules register subscriptions explicitly:

```csharp
builder.Services.AddIntegrationEventHandler<CatalogItemCreatedIntegrationEvent, CatalogItemCreatedProjectionHandler>(
    OrderingModuleMetadata.Name,
    CatalogModuleMetadata.Name);
```

The event contract owns event identity through `EventType`/`EventVersion` constants plus `IntegrationEventNameAttribute` and `IntegrationEventVersionAttribute`, scope behavior through `[ScopeAware]`, and the handler owns stable handler identity through `IntegrationEventHandlerAttribute`. Registration remains explicit and supplies consumer/producer module context.

Each subscription requires a module-owned `IInboxStore`. The runtime creates a deterministic durable consumer per handler and acknowledges a NATS message only after the handler effect and inbox processed marker are committed.

Cross-module data ownership defaults to local duplication:

- producer modules publish stable integration events from `.Contracts`;
- consumer modules store scalar external ids, not foreign keys;
- consumer modules project the fields they need into their own schema;
- decisions are made from local state.

## Consequences

This makes the consuming side at-least-once and idempotent. It also keeps modules removable: deleting Catalog internals does not require changing Ordering as long as Catalog contracts remain compatible.

The tradeoff is eventual consistency and duplicated data. That is intentional. If a consumer needs faster coherence, it can keep local TTLs short, disable local cache for affected reads, or handle more event types. It should still avoid synchronous cross-module internal reads by default.

## Guardrails

- `Ordering.*` may reference `Catalog.Contracts` only.
- Modules do not reference NATS client packages directly.
- Default hosts do not register example modules or NATS consumers.
- `Host.AdminCli` does not start hosted consumer loops.
