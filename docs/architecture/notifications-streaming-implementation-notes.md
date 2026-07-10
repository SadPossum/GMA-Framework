# Notifications And Streaming Implementation Notes

## Scope

This slice adds optional, front-door-focused user notifications and live streaming. A later pass added the optional `Notifications` module for persisted history/read state and a module-owned durable request event. Preferences, delivery receipts, email/SMS/push providers, and cross-process live fanout remain future module-owned adapters/features.

## Decisions To Preserve

- Modules depend only on `Gma.Framework.Notifications`.
- `Gma.Framework.Notifications.Infrastructure` owns publisher/runtime coordination, scoped request queues, payload serialization, fail-open history writer and sink delivery, and metrics.
- `Gma.Framework.Notifications.Cqrs` owns the optional post-commit command pipeline bridge for queued notification requests.
- `Gma.Framework.Realtime` owns generic realtime feed/sink/subscription contracts and channel naming.
- `Gma.Framework.Realtime.Infrastructure` owns the generic in-memory fanout and bounded subscriber queues.
- `Gma.Framework.Realtime.Notifications` owns the bridge from notification targets/messages to generic realtime channels and provides the `notifications.live-feed` composition feature.
- `Gma.Framework.Notifications.Api` owns the authenticated SSE endpoint.
- `Gma.Framework.Notifications.SignalR` owns SignalR hubs, groups, and browser query-string bearer-token support for the notification hub path.
- `Notifications` owns durable user notification history/read state when explicitly composed.
- `Gma.Modules.Notifications.Contracts` owns `UserNotificationRequestedIntegrationEvent`; producers may reference that contracts package and publish producer-scoped subjects through their own outbox.
- `Gma.Modules.Notifications.Application` owns the reusable notification request projector, but producer subscriptions are host/example composition. Use `AddUserNotificationRequestSubscription(producerModule)` instead of hardcoding Catalog or any other producer into the reusable module.
- `Gma.Modules.Notifications.Persistence` owns the `notifications.inbox_messages` table and idempotent consumer state.
- `Gma.Modules.Notifications.Api` exposes current-user history/read APIs and durable SSE history stream with `afterSequence`.
- `Gma.Modules.Notifications.AdminApi` exposes tenant-wide history/list/get/stream APIs behind admin RBAC.
- `Notifications` broadcast notifications are stored once and read per recipient through receipts; do not fan out broad notifications into one row per user/admin.
- Broadcast audiences are `tenant-users`, `tenant-admins`, `platform-users`, and `platform-admins`. Tenant broadcasts require tenant id; platform broadcasts must not carry tenant id.
- Broadcast list/read/stream handlers normalize tenant id, recipient kind, recipient id, and receipt scope through `NotificationBroadcastRecipientContext` before calling persistence. Public APIs may pass the `Gma.Modules.Notifications.Contracts.NotificationBroadcastRecipientKind` enum; application ports must not accept raw recipient-kind strings.
- Notification contract enums own their JSON converters. Severity values write `info`, `success`, `warning`, or `error`; broadcast audiences write lowercase kebab-case values; recipient kinds write `user` or `admin`; SSE item kinds write `notification` or `heartbeat`. Numeric, unknown, and undefined enum values should fail before reaching application handlers.
- Broadcast stream cursors are separate from direct history stream cursors. A future unified feed needs a dedicated feed cursor.
- Durable history and broadcast stream polling uses module-owned `Notifications:DurableStreams` options. Keep the option limits aligned with the query validators so bad configuration fails at startup instead of creating a silent stream loop. Poll-time query failures must log and close the stream rather than continuing forever.
- `Host.Api` can compose the adapters explicitly, but notifications are disabled by default through `Notifications:Enabled=false`. Compose `AddUserNotificationsRealtime()` before notification SSE when live streaming is enabled.
- Durable business guarantees must use existing module outbox/inbox patterns; live notifications are best-effort, and history rows are guaranteed only when projected from committed integration events.
- Tenant ids and user ids must never be metric tags. They may appear in structured logs only when useful for troubleshooting.
- SignalR groups are routing hints, not authorization. The hub must authorize the connection and derive the tenant/user group from claims, not client input.

## Audit Checklist

- No module references `Gma.Framework.Notifications.Cqrs`, `Gma.Framework.Notifications.Api`, `Gma.Framework.Notifications.SignalR`, SignalR packages, or ASP.NET notification internals.
- No module references `Gma.Framework.Realtime.Infrastructure` or `Gma.Framework.Realtime.Notifications` for notification delivery. The notification bridge belongs in host composition.
- Domain projects do not reference notifications.
- Application projects may reference `Gma.Framework.Notifications` only when they explicitly enqueue or publish front-door notifications.
- SSE requires authenticated users and tenant match when tenancy is enabled.
- SignalR requires authenticated users and tenant claim when tenancy is enabled.
- Publisher cancellation propagates; sink failures fail open and are logged/metered.
- Transactional command handlers use `IUserNotificationRequestQueue` only for best-effort live delivery after commit.
- Transactional command handlers publish `UserNotificationRequestedIntegrationEvent` through their module outbox when notification history must be guaranteed.
- If `Notifications` is composed, history writer failures fail open and do not roll back committed source commands.
- Notification streams advance by durable `StreamSequence`, not wall-clock timestamps.
- Broadcast streams advance by their own durable `StreamSequence`; do not merge direct notification and broadcast sequence values client-side.
- Platform broadcast entities are not `IScopedEntity`; visibility must stay in the broadcast repository so platform broadcasts can appear inside tenant-scoped feeds.
- Non-tenant projects should omit `TenancyModule`; the shared local default tenant id still scopes tenant broadcasts and read receipts.
- Payloads are bounded and serialized centrally.
- Durable notification payload JSON is bounded to 32 KB in the event contract, domain value object, and EF model. Provider migrations must keep that limit visible.
- Broadcast read receipts must remain idempotent under retries and concurrency. Keep SQL Server/PostgreSQL insert-if-missing behavior behind the repository, include recipient scope in the unique identity, and keep read-all bounded.
- Integration event handlers normally require local module descriptor subscriptions. Reusable fan-in handlers may use `IntegrationEventHandlerAttribute.RequiresExplicitProducerBinding` only when the producer list truly belongs to host/example composition.
- Realtime subscriber queues are bounded so disconnected or slow clients cannot grow memory unbounded.

## Follow-Up Candidates

- Notification preferences, retention, and admin controls.
- Optional Redis/Azure SignalR backplane for multi-instance delivery.
- Notification preference policy and per-channel provider adapters.
- Additional producer subscriptions for modules that need durable notification requests.
