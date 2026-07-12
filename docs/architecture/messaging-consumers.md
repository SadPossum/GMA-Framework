# Messaging Consumers

NATS consumers are optional infrastructure. Production HTTP hosts enable publishing through the configured `Gma.Framework.Messaging.Nats.Aspire` adapter with `AddConfiguredNatsJetStreamMessaging()`. Consumer-capable hosts use `AddConfiguredNatsJetStreamConsumers()`, which is a no-op unless `NatsConsumers:Enabled=true` and requires `ConnectionStrings:nats` when consumers are enabled.
Consumers start only when a host explicitly composes the consumer adapter and modules that register subscriptions.
`AddNatsJetStreamConsumers()` lives in `Gma.Framework.Messaging.Nats`, composes `AddMessagingInfrastructure()` idempotently for custom hosts, and messaging infrastructure composes only the runtime clock/id baseline it needs. It does not discover modules or subscriptions. A host still has to register each subscribing module explicitly.

## Contracts

Consumer-facing contracts live in `Gma.Framework.Messaging`:

- `IIntegrationEventHandler<TEvent>`
- `IntegrationEventSubscription`
- `IIntegrationEventSubscriptionRegistry`
- `IInboxStore`
- `InboxMessageRecord`
- `InboxProcessResult`

Application code depends on these abstractions only. It does not depend on NATS.
Create `InboxProcessResult` values through its factories. `Processed` and `Duplicate` carry no error text; `Failed` carries a bounded, normalized error string for retry diagnostics.

## Runtime

`NatsJetStreamConsumerService` lives in `Gma.Framework.Messaging.Nats`.

The service:

- validates that every subscription has a matching module-owned inbox store;
- creates a durable JetStream pull consumer per subscription;
- names durable consumers as a NATS-safe physical key shaped like `<application-namespace>-<environment>-<consumer-module>-<handler-name>`;
- deserializes messages by subscription event type;
- runs registered processing-context contributors before the handler, such as `Gma.Framework.Tenancy.Messaging.Infrastructure` setting tenant context for tenant-scoped subscriptions;
- invokes the handler through DI with a cached typed delegate;
- acknowledges only after the inbox store returns processed or duplicate;
- negatively acknowledges failed processing so JetStream can redeliver.

NATS durable names cannot use subject-style dots. Keep subjects dotted, but keep physical durable names hyphenated and derived from the stable handler name.
The consumer runtime uses reflection only when compiling a per-subscription handler invoker. Message processing then calls the cached typed delegate, preserving direct handler exceptions for inbox failure metadata and retry diagnostics. Keep this framework magic covered by focused tests whenever the invocation path changes.
Subscription metadata is validated when it is created: module names, event names, and stable handler names are lowercase kebab-case segments, and subjects follow `<application-namespace>.<module>.<event>.v<version>`. The default namespace is `gma`; configured consumers render the physical subject from `ApplicationIdentity:Namespace`.

## Options

```json
{
  "NatsConsumers": {
    "Enabled": false,
    "FetchBatchSize": 10,
    "PollInterval": "00:00:01",
    "AckWait": "00:00:30",
    "AckProgressInterval": "00:00:10",
    "MaxDeliver": 5,
    "HandlerTimeout": "00:00:30",
    "NakDelay": "00:00:01"
  }
}
```

`Enabled=false` is the default. A host may register the hosted service without starting consumers until configuration enables it. `Host.Worker` calls the configured consumer adapter only when `NatsConsumers:Enabled=true`, so disabled consumer loops stay out of the service graph.
Stream name is configured through `NatsJetStream` options and uses the same restricted portable character set as publishing: ASCII letters, digits, `-`, and `_`. If `NatsJetStream:StreamName` is blank, it is derived from `ApplicationIdentity:Namespace`.
`DurablePrefix` is optional. When it is absent, the first segment of the physical durable consumer name is derived from `ApplicationIdentity:Namespace`. It is not a subject prefix.
The runtime keeps NATS fetch expiration above the NATS client minimum even when `PollInterval` is configured below one second. Short poll intervals may still be useful for retry delays and tests, but the pull fetch window must remain valid for the client.
Consumer runtime values are validated at startup. A configured `DurablePrefix` must be a lowercase kebab-case durable-name segment, `FetchBatchSize` must be between 1 and 500, and `PollInterval`, `AckWait`, `MaxDeliver`, `HandlerTimeout`, and `NakDelay` must be positive. `AckProgressInterval` is optional, defaults to one third of `AckWait`, and must be shorter than `AckWait` when configured. Environment names used by consumer hosts must also normalize to a lowercase kebab-case durable-name segment; use values like `development`, `staging`, or `production`.

## Inbox

Each module maps `InboxMessage` into its own schema. EF-backed modules should use `ConfigureInboxMessage(...)` from `Gma.Framework.Messaging.Infrastructure`; the shared `EfInboxStore<TDbContext>` handles the common idempotency flow, but the table belongs to the module.
On success, handler effects and the inbox processed marker commit in the same transaction.
On failure, handler effects are rolled back before failure metadata is recorded.
Handler timeout cancellation is treated as a failed attempt and is negatively acknowledged for retry.
While inbox processing is active, the consumer sends periodic in-progress acknowledgements so handlers may safely run longer than the original `AckWait`. A transient progress-ack failure is logged and retried; the inbox transaction remains the idempotency boundary if the broker redelivers.
Host shutdown cancellation still propagates so consumers can stop without writing misleading failure metadata.
Inbox metadata limits are declared by `InboxMessage` and consumed by each module EF mapping. Handler names, subjects, event type names, generic message scope ids, worker ids, and last-error text are validated or bounded in shared infrastructure before persistence.

Inbox rows track:

- event id;
- handler name;
- subject;
- event type and version;
- scope id, when a cross-boundary bridge such as tenancy resolves one;
- processing status;
- attempt count;
- timestamps;
- processing owner;
- last error.

## Handler Rules

- Handlers must be idempotent.
- Handler effects and inbox completion should use the same module DbContext where possible.
- Consumers should store external ids as scalars.
- Consumers should not create database foreign keys to producer-module tables.
- Poison messages caused by deserialization failure are terminated by the runtime.
- Disabled consumers must not require a NATS connection at host startup.
- Consumer-only workers can set `NatsJetStream:Enabled=false` and `NatsConsumers:Enabled=true`; they still need `ConnectionStrings:nats`, the consuming module application, and that module's `IInboxStore`.
