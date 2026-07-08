# <EventName> Integration Event

## Purpose

Describe why this event exists and who is expected to consume it.

## Subject

```text
{application-namespace}.<module>.<event>.v<version>
```

Use `gma` only as the default local skeleton namespace. Production applications should set `ApplicationIdentity:Namespace` and render subjects through shared naming helpers or module subject factories.

## Version

`1`

## Owner

`<Module>`

## Payload

The event type should inherit `IntegrationEvent` and pass the stable event name and version to the base constructor. Do not duplicate event id, occurrence time, event name, or version properties in the module event type.
If the event is tenant-owned, inherit `TenantIntegrationEvent` from `Gma.Framework.Tenancy.Messaging` instead and pass tenant id to that base type. Hosts that publish or consume tenant-owned events must call `AddTenantAwareMessaging()`.

```json
{
  "eventId": "guid",
  "occurredAtUtc": "timestamp"
}
```

Tenant-owned payloads additionally include:

```json
{
  "tenantId": "string"
}
```

## Semantics

Describe what fact the event represents.

## Compatibility Rules

- Prefer additive changes.
- Do not rename existing fields.
- Do not change field meaning.
- Create a new version for breaking changes.

## Producers

- `<Module>.Application`

## Consumers

- `<ConsumerModule>` handles this event with `<handler-name>` and stores idempotency in `<schema>.inbox_messages`.

If no consumer exists yet, write `No consumers yet` and include the intended compatibility promise or reason the event is currently producer-only. Do not leave this section as unknown.

For each consumer, document:

- stable lowercase kebab-case handler name;
- durable consumer name shape: `<application-namespace>-<environment>-<consumer-module>-<handler-name>`;
- inbox table/schema;
- local projection/table updated;
- idempotency key;
- retry behavior;
- compatibility expectation.

## Tests

List tests proving the event is written to outbox, published, consumed idempotently, and projected by at least one consumer when applicable.
