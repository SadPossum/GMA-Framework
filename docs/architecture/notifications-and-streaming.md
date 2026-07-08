# Notifications And Streaming

Notifications are optional front-door delivery for authenticated users. They are not a backend event bus, not a CQRS dispatcher, and not a replacement for outbox/NATS integration.

Use notifications for best-effort UI updates such as operation completion, task progress hints, or "refresh this view" messages. Use integration events and the outbox for durable module facts.

## Boundaries

Modules may depend on the contracts in `Gma.Framework.Notifications` for best-effort front-door delivery:

- `IUserNotificationPayload`
- `IUserNotificationRequestQueue`
- `IUserNotificationPublisher`
- `IUserNotificationHistoryWriter`
- `UserNotificationTarget`
- `NotificationPublishOptions`
- notification metadata attributes
- module descriptor notification metadata

Modules may also reference `Gma.Modules.Notifications.Contracts` when they intentionally publish durable notification requests for the optional `Notifications` module.

Modules must not reference:

- `Gma.Framework.Notifications.Api`
- `Gma.Framework.Notifications.Cqrs`
- `Gma.Framework.Notifications.SignalR`
- `Gma.Modules.Notifications.Application`
- `Gma.Modules.Notifications.Domain`
- `Gma.Modules.Notifications.Persistence`
- `Gma.Modules.Notifications.Api`
- `Gma.Modules.Notifications.AdminApi`
- SignalR packages
- ASP.NET notification endpoint or hub internals

The host selects delivery adapters.

## Project Split

```text
Gma.Framework.Notifications
  contracts and metadata

Gma.Framework.Notifications.Infrastructure
  in-process feed, bounded queues, serialization, metrics, fail-open history and sink dispatch

Gma.Framework.Notifications.Cqrs
  post-commit command pipeline bridge for queued notification requests

Gma.Framework.Notifications.Api
  authenticated SSE endpoint

Gma.Framework.Notifications.SignalR
  authenticated SignalR hub and group delivery

Notifications
  optional persisted history/read-state module and module-owned durable request event
```

`Host.Api` composes the pieces explicitly:

```csharp
builder.AddUserNotificationsCqrs();
builder.AddUserNotificationServerSentEvents();
builder.AddUserNotificationSignalR();

app.MapUserNotificationServerSentEvents();
app.MapUserNotificationSignalR();
```

All shared adapter calls are safe in the default host because `Notifications:Enabled=false` disables live runtime delivery. The persisted `Notifications` module is separate and is registered only when an application wants history/read state.

## Payload Metadata

Payloads own their notification identity:

```csharp
[NotificationName("catalog.item-updated")]
[NotificationVersion(1)]
[NotificationDescription("Catalog item updated user notification.")]
public sealed record CatalogItemUpdatedNotification(Guid ItemId) : IUserNotificationPayload;
```

Rules:

- notification names are normalized to lowercase dotted segments;
- versions start at `1` and are incremented when the payload contract changes incompatibly;
- descriptions are required for module metadata and docs;
- payloads are normalized JSON and are bounded by `Notifications:MaximumPayloadBytes` for live publishing and by the `Notifications` module's 32 KB durable payload limit;
- payloads must not contain passwords, access tokens, refresh tokens, token hashes, or raw secrets.

Declare public notification contracts in the owning module contracts project when another front door, tool, or module descriptor needs to know about them. Keep private one-off payloads inside the application project only when they are not public contracts.

## Requesting From Commands

Transactional command handlers may enqueue best-effort live notification intent through `IUserNotificationRequestQueue`. The queue is scoped to the current command execution, and `Gma.Framework.Notifications.Cqrs` flushes it only after a successful command result and unit-of-work commit:

```csharp
await notificationRequests.EnqueueAsync(
    CatalogModuleMetadata.Name,
    UserNotificationTarget.User(tenantId, userId),
    new CatalogItemUpdatedNotification(item.ItemId, item.Sku, item.Name, item.Status),
    new NotificationPublishOptions(
        title: "Catalog item updated",
        severity: NotificationSeverity.Info),
    cancellationToken);
```

This prevents a user from seeing "item updated" before the database commit succeeds. The scoped queue is not itself durable. If the optional `Notifications` module is composed, history is stored when the post-commit publish request reaches `IUserNotificationPublisher`; if a process dies after the source commit but before the queue flushes, no history row is created.

For guaranteed history creation, publish `UserNotificationRequestedIntegrationEvent` from the source module's outbox. The event contract lives in `Gma.Modules.Notifications.Contracts`, while the physical subject remains producer-scoped:

```text
{application-namespace}.{producer-module}.user-notification-requested.v1
```

The optional `Notifications` module can consume that event through its own inbox and write history in the `notifications` schema, but it does not subscribe to any producer by default. A host/example that wants durable notification request ingestion composes the producer binding explicitly:

```csharp
builder.Services.AddUserNotificationRequestSubscription(OrderingModuleMetadata.Name);
```

The helper derives a producer-specific durable handler name such as `ordering-notification-request`, so multiple producers can be added without sharing one consumer identity.

## Publishing From Runtime Code

Front doors, workers, and post-commit runtime code may publish through `IUserNotificationPublisher` when the notification is already safe to deliver. Do not inject adapters or SignalR into application/domain code.

Publishing is best-effort. If notifications are disabled and no history writer is registered, the publisher records a bypass metric and returns. If a history writer is registered, the publisher still stores history before bypassing live delivery. History-writer and live-sink failures are logged and fail open; they do not fail an already successful business operation. Caller cancellation and payload serialization errors still propagate.

For durable facts, raise domain events and write integration events through the module outbox. A notification can be emitted as a user-facing side effect, but it should not be the authoritative record.

## Persisted History

Compose the optional `Notifications` module when users need notification history or read/unread state:

```csharp
builder.AddModule<NotificationsModule>();
builder.AddAdminApiModule<NotificationsAdminApiModule>();
```

The module owns the `notifications` schema, provider-split migrations, current-user endpoints under `/api/notifications`, and admin endpoints under `/api/admin/notifications`.

Current-user history streams use:

```text
/api/notifications/history/stream?afterSequence=<last-seen-sequence>
```

Admin history streams use:

```text
/api/admin/notifications/history/stream?afterSequence=<last-seen-sequence>&userId=<optional-user>
```

When `afterSequence` is omitted, the stream starts after the current maximum durable sequence and behaves like a live stream. When supplied, the stream replays committed rows with a greater sequence. If the initial cursor lookup fails, the endpoint returns the application error. If a later poll fails after the response has started, the module logs the error and closes the stream so clients can reconnect/back off instead of sitting on a silent broken feed.

The module can be fed either by the shared best-effort publisher or by module-owned durable request events consumed through NATS/inbox. Prefer the durable event path for notifications that must survive source-process crashes.

## Broadcast Notifications

The `Notifications` module also supports durable broadcast notifications for broad audiences without fan-out writes to every possible recipient. Broadcasts are stored once in `notification_broadcasts`; recipient read state is stored separately in `notification_broadcast_reads`.

Supported audiences:

- `tenant-users`
- `tenant-admins`
- `platform-users`
- `platform-admins`

Tenant broadcasts require a tenant id and are visible only inside that tenant. Platform broadcasts have no tenant id and are visible across tenant contexts to the matching recipient kind. In non-tenant projects, omit `TenancyModule`; the shared default tenant context is still used as the local tenant scope for tenant broadcasts. Read receipts use opaque recipient ids (`user` or `admin`) and do not reference Auth or Administration tables.

Read receipts are idempotent per `(broadcast, recipient scope, recipient kind, recipient id)`. The recipient scope is the current tenant context when present, or a global scope when tenancy is not active; this keeps platform broadcast read state from crossing tenants that happen to reuse an opaque user/admin id. SQL Server uses an insert-if-missing statement guarded by update/hold locks; PostgreSQL uses `ON CONFLICT DO NOTHING`; non-relational tests use the same repository contract through EF tracking. `read-all` processes broadcasts in bounded batches instead of loading the full visible backlog into memory.

Broadcasts intentionally use separate stream cursors from direct user history:

```text
/api/notifications/broadcasts/stream?afterSequence=<last-seen-broadcast-sequence>
/api/admin/notifications/broadcasts/inbox/stream?afterSequence=<last-seen-broadcast-sequence>
```

Do not combine direct history `StreamSequence` and broadcast `StreamSequence` into one client cursor. A future unified feed should introduce an explicit feed cursor model instead of overloading either sequence.

Admin broadcast management is split by scope:

```text
POST /api/admin/notifications/broadcasts
POST /api/admin/notifications/platform-broadcasts
```

Tenant broadcast management requires a tenant-scoped admin grant. Platform broadcast management runs without tenant context and requires a global grant for the same broadcast permission.

## Delivery Adapters

### SSE

The SSE adapter maps an authenticated stream endpoint at `Notifications:Sse:StreamPath`, default:

```text
/api/notifications/stream
```

The endpoint requires authorization and tenant context. When tenancy is enabled, the tenant claim on the token must match the active tenant context. Messages are emitted as typed SSE items and heartbeats keep long-lived clients from appearing idle.

### SignalR

The SignalR adapter maps an authenticated hub at `Notifications:SignalR:HubPath`, default:

```text
/hubs/notifications
```

The hub derives tenant/user routing from claims and joins the connection to a server-owned hashed group. Clients do not choose group names. The adapter supports the common browser SignalR pattern of reading a bearer token from the configured query-string parameter only for the notification hub path.

SignalR is not used for CQRS commands, query dispatch, or backend module integration.

## Configuration

Default:

```json
{
  "Notifications": {
    "Enabled": false,
    "SubscriberQueueCapacity": 128,
    "MaximumPayloadBytes": 32768,
    "Sse": {
      "Enabled": true,
      "StreamPath": "/api/notifications/stream",
      "NotificationEventType": "notification",
      "HeartbeatInterval": "00:00:15"
    },
    "SignalR": {
      "Enabled": true,
      "HubPath": "/hubs/notifications",
      "ClientMethodName": "notification",
      "AccessTokenQueryParameter": "access_token"
    },
    "DurableStreams": {
      "BatchSize": 25,
      "PollInterval": "00:00:01"
    }
  }
}
```

Configuration validation fails startup for invalid paths, event names, method names, queue sizes, payload limits, or heartbeat intervals. Runtime delivery failures fail open.

`Notifications:DurableStreams` belongs to the optional persisted `Notifications` module. `BatchSize` controls how many committed history or broadcast rows each stream poll reads, and `PollInterval` controls the polling cadence. The batch size must stay between 1 and 100; the poll interval must stay between 250 milliseconds and 1 minute.

## Metrics

Notification metrics use the `{ApplicationIdentity:Namespace}.notifications` meter. The skeleton default namespace is `gma`, but applications should set `ApplicationIdentity:Namespace` before production deployment.

Metric tags stay bounded:

- `module`
- `operation`
- `provider`
- `result`

Tenant ids, user ids, notification ids, and payload fields must not be metric tags. They may be included in structured logs when needed for troubleshooting.

## Multi-Instance Behavior

The live adapters use in-process fanout. In a single API instance, SSE and SignalR receive messages published by that process. In multiple API instances, delivery reaches connections on the same process unless the deployment adds sticky sessions, a backplane, Azure SignalR, or a replay path from the optional notification history module.

Do not enable notifications for business-critical delivery until the chosen deployment topology can tolerate missed live messages or replay from a durable source.

## Testing Rules

Add unit tests for:

- payload metadata validation;
- `UserNotificationRequestedIntegrationEvent` validation and subject shape;
- disabled delivery bypass;
- bounded subscriber queues;
- fail-open sink behavior.
- persisted history writer fail-open behavior.
- stream cursor behavior through durable `StreamSequence`.
- broadcast audience visibility and per-recipient read receipts.

Add integration tests for:

- authenticated SSE delivery;
- tenant mismatch rejection;
- authenticated SignalR delivery.
- Notifications inbox consumption from a real published request event when a runtime composes the module consumer.

Architecture tests must continue proving that modules do not reference front-door notification adapters or SignalR packages directly.
