# Messaging and Outbox

Messaging is behind abstractions. Domain and application code should never depend on NATS directly.

## Abstractions

- `IIntegrationEvent`
- `IntegrationEvent`
- `IntegrationEventEnvelope`
- `IOutboxWriter`
- `IOutboxWriterRegistry`
- `IOutboxStore`
- `IEventBus`

Public module integration events inherit `IntegrationEvent` from `Gma.Framework.Messaging`.
The base owns event id, occurrence time, event name, and version validation. Module event types keep payload-specific fields and validation local to the owning `.Contracts` project.
Scope-owned events opt into generic isolation by inheriting `ScopedIntegrationEvent` or implementing `IScopedIntegrationEvent`; that contract owns scope id normalization without making base messaging or NATS depend on tenancy. Tenancy-specific contracts may still use `Gma.Framework.Tenancy.Messaging` as a bridge when their public language must be tenant-shaped.
This keeps the skeleton compatible with common event metadata practices without forcing a full CloudEvents envelope into module payload classes.

## Runtime Adapter

`Gma.Framework.Messaging.Infrastructure` registers EF outbox/inbox helpers, the outbox writer registry, outbox options, messaging metrics, the outbox publisher loop, and a fail-fast null event bus. It does not reference NATS and does not start the outbox publisher by itself.
`Gma.Framework.Messaging.Nats` owns the NATS JetStream event bus, consumer hosted service, NATS options, and low-level `AddNatsJetStreamMessaging()` / `AddNatsJetStreamConsumers()` composition hooks.
`Gma.Framework.Messaging.Nats.Aspire` owns configured Aspire NATS client composition for production-style hosts. `AddConfiguredNatsJetStreamMessaging()` wires publishing and `AddConfiguredNatsJetStreamConsumers()` wires consumer-only or consumer-plus-publisher hosts without requiring direct Aspire package references in composition roots.
`Gma.Framework.Tenancy.Messaging` is the optional contract bridge for module contract packages that need tenant-aware integration events.
`Gma.Framework.Tenancy.Messaging.Infrastructure` is the optional runtime bridge for hosts; `AddTenantAwareMessaging()` registers generic scope resolution and consumer tenant-context setup through messaging extension points.

HTTP hosts that need real publishing opt in by referencing `Gma.Framework.Messaging.Nats.Aspire` and calling:

```csharp
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging(); // only for tenant-aware hosts that consume scope-aware integration events
builder.AddConfiguredNatsJetStreamMessaging();
```

That call is a no-op unless `NatsJetStream:Enabled=true`. When enabled, it validates `NatsJetStream` settings, requires `ConnectionStrings:nats`, configures the Aspire NATS client from that connection string, calls into `Gma.Framework.Messaging.Nats`, registers the NATS event bus, and starts `OutboxPublisherService`.
When the host does not opt in, `IEventBus` remains a null adapter and module outbox rows stay local until a publisher is composed.
Tools and short-lived hosts can use shared infrastructure without accidentally draining outboxes.

`Host.Worker` is the optional separated publishing process. A simple deployment can skip it and let `Host.Api` or `Host.AdminApi` publish outbox rows by setting `NatsJetStream:Enabled=true` in those HTTP hosts. A separated deployment sets HTTP hosts to `NatsJetStream:Enabled=false`, composes the required module persistence in `Host.Worker` with `Worker:Modules:*`, and enables `NatsJetStream:Enabled=true` only in the worker.

```text
Simple mode:
  Host.Api -> AddConfiguredNatsJetStreamMessaging()
  Host.Worker is not deployed

Separated mode:
  Host.Api -> AddMessagingInfrastructure(), NatsJetStream:Enabled=false
  Host.Worker -> Worker:Modules:Auth=true, NatsJetStream:Enabled=true
```

The worker can publish only module outbox stores registered in that worker process. For example, an Auth-only worker sets `Worker:Modules:Auth=true`; a Catalog/Ordering worker must opt into those modules and run their migrations before enabling publishing.

Lower-level test or custom hosts can still reference `Gma.Framework.Messaging.Nats`, provide `INatsConnection` themselves, and call `AddNatsJetStreamMessaging()` directly, but production hosts should use the configured Aspire adapter so connection-string behavior stays consistent.
The low-level messaging methods compose `AddMessagingInfrastructure()` idempotently, and that composes `AddRuntimeInfrastructure()` for shared clocks and id generation without pulling in CQRS or domain-event dispatch.

The NATS JetStream adapter publishes each outbox row with the outbox message id as `NatsJSPubOpts.MsgId`.
If the broker accepted a message but the local outbox mark-processed step failed, a later retry may publish the same outbox row again. JetStream duplicate tracking then returns a duplicate ack instead of storing another message, and the adapter treats that ack as a successful idempotent publish.
Consumers must still keep inbox idempotency because delivery remains at-least-once.

## Subject Format

Integration event subjects use:

```text
{application-namespace}.{module}.{event}.v{version}
```

Example:

```text
gma.auth.member-registered.v1
```

`gma` is the default `ApplicationIdentity:Namespace` for this skeleton. Set a project-specific namespace such as `acme-orders` before creating production streams, durable consumers, cache keys, or dashboards. Module contracts should keep stable logical module/event/version names and render physical subjects through `IntegrationEventNaming` or the module's subject factory methods.

## Outbox Flow

```text
Command handler
  -> aggregate raises domain event
  -> unit of work dispatches domain event
  -> domain event handler resolves module outbox writer
  -> module outbox writer stores integration event
  -> EF Core commits aggregate and outbox message
  -> OutboxPublisherService claims pending messages
  -> IEventBus publishes envelope
  -> outbox message marked processed
```

## Outbox Options

```json
{
  "Outbox": {
    "BatchSize": 25,
    "PollIntervalMilliseconds": 5000,
    "LockDurationMilliseconds": 60000,
    "MaxAttempts": 10
  }
}
```

Outbox runtime values are validated at startup. `BatchSize`, `PollIntervalMilliseconds`, `LockDurationMilliseconds`, and `MaxAttempts` must be positive.

NATS stream options:

```json
{
  "NatsJetStream": {
    "Enabled": false
  }
}
```

`StreamName` is optional. When it is absent, infrastructure derives a stream name from `ApplicationIdentity:Namespace`, for example `gma` becomes `GMA_EVENTS` and `acme-orders` becomes `ACME_ORDERS_EVENTS`. Override `NatsJetStream:StreamName` only when an existing broker naming policy requires it. The skeleton intentionally accepts only ASCII letters, digits, `-`, and `_` for stream names. That follows the portable subset of [NATS JetStream naming guidance](https://docs.nats.io/nats-concepts/jetstream/streams): stream names must not contain whitespace, `.`, `*`, `>`, path separators, or non-printable characters.

## Claiming and Retry

The outbox supports:

- batch claims;
- worker ownership;
- lock expiration and reclaim;
- retry scheduling;
- max-attempt exhaustion;
- wrong-worker mark prevention.

Publisher failures are isolated at module-store and message granularity. If one module outbox store cannot claim rows, the publisher logs that store failure and continues with the other registered stores. If publishing one message fails, the publisher records the failed attempt for that row and leaves the rest of the batch eligible for normal processing. A publish operation that is canceled before host shutdown is treated as a failed attempt; host shutdown cancellation still stops the background service without rewriting outbox state.

Broker-side publish de-duplication is a defensive layer, not a replacement for outbox state. The local outbox row remains the source of retry truth, and consumer inbox tables remain the source of handler idempotency truth.

Outbox metadata limits are declared by `OutboxMessage` and applied through `ConfigureOutboxMessage(...)` from `Gma.Framework.Messaging.Infrastructure`. Subject, event type, generic message scope id, worker id, and bounded failure metadata should fail or truncate in shared infrastructure before a provider-specific `SaveChanges` path can fail late.
For storage compatibility, the shared EF mapping currently stores `ScopeId` in the existing `TenantId` column. New code should use `ScopeId` for messaging infrastructure and `TenantId` only inside tenant-aware event payloads or tenant modules.

## Inbox And Consumers

NATS consumers are optional and documented in [Messaging Consumers](messaging-consumers.md).

Each consuming module owns an inbox table in its schema. The shared consumer runtime acknowledges a NATS message only after the handler effect and inbox processed marker commit.
EF-backed modules should map inbox rows through `ConfigureInboxMessage(...)` so keys, indexes, and length limits stay aligned across modules without repeating configuration blocks.

## Messaging Guidelines

- Publish integration events only through outbox.
- Use `IOutboxWriterRegistry` from application handlers; do not inject a bare `IOutboxWriter`.
- Keep event names stable.
- Version events explicitly.
- Inherit scope-owned public events from `ScopedIntegrationEvent` or implement `IScopedIntegrationEvent`, then compose `Gma.Framework.Tenancy.Messaging.Infrastructure` in tenant-aware hosts that publish or consume them.
- Inherit tenant-free public events from `IntegrationEvent`; do not re-declare event id, occurrence time, event name, or version in every event.
- Prefer additive event changes.
- Do not expose internal domain entities as integration events.
- Consumers should reference producer `.Contracts` only and duplicate local read data when they need it for decisions.
