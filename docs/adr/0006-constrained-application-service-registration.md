# ADR 0006: Constrained Application Service Registration

## Status

Accepted.

## Context

Module application projects repeatedly registered the same mechanical service shapes:

- `ICommandHandler<TCommand,TResponse>`;
- `IQueryHandler<TQuery,TResponse>`;
- `ICommandValidator<TCommand>`;
- `IQueryValidator<TQuery>`;
- `IDomainEventHandler<TEvent>`.

The explicit lists were easy to audit, but every new command, query, validator, or domain-event projector required a second edit in dependency injection. That created a small but persistent source of drift in optional modules and generated module shells.

The project still avoids implicit module discovery. Host composition must remain explicit so optional modules stay removable and interchangeable.

## Decision

Add a small `Gma.Framework.Application.Composition` helper:

```csharp
services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);
```

The helper scans only the assembly passed by the module's own application registration method. It registers concrete closed implementations of the supported CQRS and domain-event handler/validator interfaces as scoped services through `TryAddEnumerable`.

Do not use a general-purpose scanning package for this slice. `Scrutor` is a good library for broad assembly scanning and decoration, but this skeleton currently needs a tiny, documented rule rather than a new runtime dependency or open-ended convention engine. The helper still uses the normal `Microsoft.Extensions.DependencyInjection` service descriptor model and `TryAddEnumerable` semantics.

Integration-event consumers stay explicit:

```csharp
services.AddIntegrationEventHandler<CatalogItemCreatedIntegrationEvent, CatalogItemCreatedProjectionHandler>();
```

Published event metadata lives on the event contract, and consumer module/handler identity lives on the handler type. Subscription registration is still an explicit composition call, not automatic assembly scanning.

## Consequences

Adding a new command, query, validator, or domain-event projector now needs only the type itself when it follows the supported interfaces.

The tradeoff is a small amount of reflection. It is deliberately constrained:

- no host-level assembly scanning;
- no automatic module registration;
- no integration-event subscription discovery;
- deterministic descriptor order;
- repeat-safe registration.

If startup performance, AOT trimming, or stronger compile-time discovery becomes a real concern, replace this helper with a source generator or explicit generated registration while preserving the same public module composition rule.

## Guardrails

- Module application registration remains an explicit `IServiceCollection` extension.
- Hosts still choose modules through explicit composition.
- Integration-event subscriptions use explicit metadata registration.
- Tests cover registered interface shapes, ignored unsupported types, idempotency, and default-container resolution.
- Architecture tests guard module application registration and scaffolding against drifting away from this bounded rule.
