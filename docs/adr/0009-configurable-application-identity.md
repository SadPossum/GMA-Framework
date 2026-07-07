# 0009 Configurable Application Identity

## Status

Accepted.

## Context

The skeleton is intended to be reused for different business applications. Earlier versions used `gma` and `GenericModularApi` directly in several physical identifiers: NATS subjects and stream defaults, durable consumer names, cache keys, metric names, CLI display text, JWT defaults, and a few compatibility helper names.

That made local development easy, but it also made the skeleton feel branded after a team started building a real product on top of it.

## Decision

Hosts expose one shared `ApplicationIdentity` configuration section:

```json
{
  "ApplicationIdentity": {
    "DisplayName": "GenericModularApi",
    "Namespace": "gma"
  }
}
```

`DisplayName` is human-facing and can be used for CLI text and default JWT issuer/audience values. `Namespace` is a lowercase kebab-case runtime namespace used to derive physical identifiers.

The default namespace remains `gma` for compatibility. New applications should set a product-specific namespace before creating production NATS streams, durable consumers, cache keys, dashboards, or alerts.

Shared infrastructure derives these defaults from `ApplicationIdentity:Namespace`:

- integration-event subject prefix;
- NATS stream name when `NatsJetStream:StreamName` is not set;
- NATS durable prefix when `NatsConsumers:DurablePrefix` is not set;
- cache key prefix when `Caching:KeyPrefix` is not set;
- application-owned meter and instrument names.

Adapter-specific physical overrides remain available when a deployment must integrate with existing broker/cache naming policies.

## Consequences

- Module contracts keep stable logical module/event/version names and render physical subjects through shared naming helpers or module subject factory methods.
- Runtime hosts can be cloned into a new product without editing module internals.
- Existing code that references `gma` defaults continues to work locally.
- Compatibility wrappers such as `AddSharedOpenApi`, `UseSharedSerilogRequestLogging`, and `GmaClaimNames` may remain temporarily, but new host composition and docs should use GMA framework names.
- Architecture and unit tests should cover both default `gma` behavior and a configured namespace such as `acme-orders`.
