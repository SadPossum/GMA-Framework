# 0013 Tagged Durable Notification Delivery

## Status

Accepted.

## Context

The original notification publisher and optional Notifications module supported best-effort live delivery plus durable web history. Products need configurable domain categories, user preferences, email/push/mobile pipelines, delivery receipts, and operator recovery without coupling producer modules to provider SDKs or turning Notifications persistence into a profile/credential store.

A generic "channel" string would mix product taxonomy, delivery intent, provider selection, and user policy. Invoking the same sink from both the live publisher and a durable worker would also risk duplicate sends without an explicit mode boundary.

## Decision

Use two canonical tag namespaces:

- `delivery:*` expresses requested delivery pipelines;
- `domain:*` expresses product meaning.

Framework contracts own tag normalization, the `respect-preferences`/`mandatory` policy, structured adapter outcomes, and explicit `BestEffort`/`Durable` sink modes. They do not own persistence, provider SDKs, destinations, templates, or product policy.

Best-effort publication has an optional policy-evaluator seam. When evaluators are composed, a matching delivery tag is sent only when every evaluator allows it; evaluator failures suppress that tag rather than bypassing persisted product policy. The Notifications module contributes an evaluator backed by its durable delivery plan, so a disabled user preference also suppresses live SignalR/realtime delivery. Hosts without policy evaluators preserve the framework-only best-effort behavior.

The optional Notifications module owns tenant-scoped tag definitions, user preferences, delivery routes, jobs, leases, retry bounds, immutable attempts, receipts, retention, user APIs, and audited admin APIs. V2 durable requests include typed tags and policy. V1 requests remain consumable and are projected to a delivered web-inbox audit row.

Planning is deterministic. An explicit active route wins. Without one, exactly one compatible durable provider may be selected. Zero or multiple providers create an auditable `unroutable` job. Inactive tags fail closed. `mandatory` bypasses user preference suppression but not inactive configuration.

Delivery is at least once. The database prevents duplicate plans for `(notification, delivery tag, provider)`. Workers claim bounded batches using expiring leases, retry with bounded exponential backoff, and retain immutable attempts. Adapters receive the stable delivery id and use it as the external idempotency key.

Add `Gma.Framework.Email` as a minimal provider-neutral `IEmailSender` boundary. The optional email notification adapter resolves a user's destination at attempt time through an application-owned resolver and renders through a replaceable template seam. Notifications persistence never stores the resolved address or vendor credentials.

## Consequences

Producer modules depend only on notification contracts and choose recipients, tags, policy, and safe content. Products compose vendor adapters, destination resolvers, templates, and secrets at the host boundary.

Web history, email, push, and future pipelines share one planner and audit model without hard-coded cross-module references. Operator configuration cannot silently choose between multiple providers, and disabled tags cannot be bypassed by mandatory requests.

At-least-once delivery means provider adapters must honor the delivery idempotency key. Deployment owners must choose worker capacity, connection-pool sizing, retention, alerts, and provider rate limits. Realtime SSE/SignalR remains best effort and requires a deployment-specific multi-instance strategy when used across replicas.
