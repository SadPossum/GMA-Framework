# 0011 Optional User Notifications And Streaming

## Status

Accepted.

## Context

The skeleton already has reliable integration events through module outboxes, NATS JetStream publishing, and consumer inboxes. That pipeline is for backend module integration and operational durability.

Some applications also need a front-door way to tell the currently connected user that something changed. Examples include operation completion, background-task progress, projection-rebuild notices, or "refresh this view" hints. Those messages should not turn SignalR, SSE, or WebSockets into a second CQRS path or a module-to-module event bus.

The implementation should stay optional, adapter-shaped, and easy to remove from applications that do not need live UI updates.

## Decision

Add a small notification contract package plus optional delivery adapters:

- `Gma.Framework.Notifications` owns user-notification contracts, payload metadata attributes, logical target/message types, publish options, module descriptor metadata, and options.
- `Gma.Framework.Notifications.Infrastructure` owns the in-memory feed, bounded subscriber queues, central payload serialization, fail-open sink delivery, and notification metrics.
- `Gma.Framework.Notifications.Cqrs` owns the post-commit command pipeline bridge that flushes queued notification requests after a successful unit-of-work commit.
- `Gma.Framework.Notifications.Api` owns the authenticated Server-Sent Events endpoint.
- `Gma.Framework.Notifications.SignalR` owns the authenticated SignalR hub, per-user group routing, and hub-scoped bearer-token query-string support.
- `Host.Api` composes these adapters explicitly, while `Notifications:Enabled=false` keeps delivery disabled by default.

SignalR and SSE are front-door delivery transports only. Backend module communication continues to use contracts, integration events, outbox/inbox, and NATS.

Notification payload identity is owned by payload types through attributes:

```csharp
[NotificationName("catalog.item-discontinued")]
[NotificationVersion(1)]
[NotificationDescription("Catalog item discontinued user notification.")]
public sealed record CatalogItemDiscontinuedNotification(Guid ItemId) : IUserNotificationPayload;
```

Modules that request best-effort live user notifications depend only on `Gma.Framework.Notifications`. Transactional command handlers enqueue through `IUserNotificationRequestQueue`; front doors and runtime code that are already outside the database commit may call `IUserNotificationPublisher`. Modules must not reference `Gma.Framework.Notifications.Cqrs`, SignalR, SSE, ASP.NET notification adapter projects, or transport packages. Durable notification history can be added through a separate optional module contract/event, not by making SignalR/SSE a backend bus.

## Consequences

This keeps live user updates useful without weakening the modular architecture:

- durable facts still go through outbox and NATS;
- live delivery remains best-effort and bounded;
- tenants and users are authorization/routing inputs, not metric tags;
- slow or disconnected clients cannot grow memory without bounds;
- alternative future transports can be added as new adapters behind `IUserNotificationSink`;
- durable notification history, preferences, delivery receipts, email, SMS, and push providers can be added as module-owned persistence/adapters.

The tradeoff is that the first slice is in-process fanout. Multi-instance deployments need either sticky sessions, an external SignalR backplane, Azure SignalR, or persisted history/replay. That is deliberate: the skeleton should not require a real-time backplane for applications that do not need it.

## Follow-Ups

- Extend the optional notification history module when product requirements include preferences, retention, delivery receipts, additional producer subscriptions, or audit.
- Add optional Redis or Azure SignalR fanout only when multi-instance live delivery becomes a real requirement.
- Add typed module examples that enqueue notifications from existing transactional command handlers.
