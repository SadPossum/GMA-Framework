# Realtime

`Gma.Framework.Realtime` is the small framework layer for live, in-process fanout. It is not a business event bus, not a CQRS transport, and not a persistence mechanism.

Use realtime for front-door delivery where a connected client can safely miss a message and recover from an authoritative source. Examples include notification live delivery, task progress hints, upload progress, or "refresh this view" signals.

## Package Split

```text
Gma.Framework.Realtime
  RealtimeChannel
  IRealtimeSink<TMessage>
  IRealtimeFeed<TMessage>
  IRealtimeSubscription<TMessage>
  RealtimeSubscriberQueueOptions

Gma.Framework.Realtime.Infrastructure
  in-memory fanout and bounded subscriber queues

Gma.Framework.Realtime.Notifications
  bridge from notification contracts to realtime channels
```

`Gma.Framework.Realtime` intentionally has no SignalR, SSE, ASP.NET, tenant, notification, task, file, or persistence dependency. Feature-specific packages own the bridge from their domain contract to a realtime channel.

## Composition Rule

Feature code should usually depend on its feature contract, not on realtime directly. For notifications, modules enqueue or publish through `IUserNotificationRequestQueue` or `IUserNotificationPublisher`; the host composes `AddUserNotificationsRealtime()` when live delivery is needed.

Future features should follow the same shape:

```text
Feature contract package
  -> feature-specific publisher/feed abstraction

Gma.Framework.Realtime.<Feature>
  -> maps feature targets/messages to RealtimeChannel
  -> registers IRealtimeSink<TMessage> / IRealtimeFeed<TMessage>

Host
  -> opts into the feature bridge explicitly
```

Do not make `Gma.Framework.Realtime` understand users, tenants, notifications, files, tasks, or authorization. Those concepts belong in feature contracts or explicit bridge packages.

## Channel Rules

Channels are logical routing names plus one or more normalized routing segments. The name uses kebab-case, while routing segments are normalized centrally and joined into an internal key.

```csharp
RealtimeChannel.Create("files-upload", tenantId, uploadId);
RealtimeChannel.Create("tasks-run", tenantId, taskRunId);
```

Do not put secrets, access tokens, refresh tokens, raw tenant policy data, or unbounded user input into channel names or segments. Channel names are routing details, not authorization. Front-door adapters still must authenticate and authorize subscriptions before exposing a stream.

`RealtimeChannel` is an immutable value object. Two channels with the same normalized name and routing segments compare equal and route to the same in-memory subscriber set.

## Delivery Semantics

The default infrastructure is in-memory and best-effort:

- delivery reaches only subscribers in the same process;
- slow subscribers use bounded queues that drop oldest messages;
- disconnected clients miss messages;
- cancellation is respected;
- durable recovery must come from a module-owned read model, outbox/inbox projection, or another authoritative source.

Subscriber queues default to 128 messages and must be configured between 1 and 10000. Invalid queue configuration fails options validation instead of being silently coerced at runtime.

For multi-instance deployments, use sticky sessions, an external backplane, a provider-specific realtime adapter, or a durable replay path when missing live messages is not acceptable.

## Testing Rules

Add tests for:

- channel validation and normalization;
- matching-channel delivery only;
- bounded queue behavior for slow subscribers;
- feature bridge registration and composition-feature requirements;
- module architecture guards proving modules do not reference generic realtime infrastructure unless the module intentionally owns a realtime adapter.
