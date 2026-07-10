# Metadata Attribute Pipeline Refactor Task

## Summary

Refactor the current metadata-attribute experiment into a package-owned metadata pipeline. Keep the explicit `ModuleDescriptor` builder and explicit host/module composition, but split large attributes into small capability-owned attributes so new optional features do not drag unrelated concepts such as tenancy, task control, worker routing, subject prefixes, or module identity into every descriptor package.

This task supersedes the current fat-attribute draft where attributes such as `ModulePublishedEventAttribute`, `IntegrationEventHandlerAttribute`, and `ModuleTaskAttribute` carry too much cross-cutting metadata.

## Problem

The broad direction is good: metadata that belongs to a local type should live near that type, and module descriptors should be able to read that metadata through explicit helper calls.

The current shape is too coarse:

- messaging attributes know about tenancy;
- task attributes know about tenancy, worker groups, task kind, task control, descriptions, and payload version in one bundle;
- published-event attributes carry module identity and physical subject prefix;
- handler attributes carry consumer module identity;
- every new optional feature would be tempted to add another field to existing descriptors or attributes, slowly pulling the whole stack into packages that should stay small.

That conflicts with the project holy grail: optional modules, optional infrastructure, package-level replaceability, and explicit composition without broad magic.

## Architectural Direction

Use small attributes owned by the package that understands the metadata.

Examples:

- `Gma.Framework.Messaging` owns event and consumer-handler identity only.
- `Gma.Framework.Tasks` owns task payload identity, payload version, kind, worker routing, and task-control metadata only if those concepts remain in `Gma.Framework.Tasks`.
- `Gma.Framework.Tenancy` owns tenant semantics such as tenant-scoped or tenant-independent markers.
- A future small shared metadata package may own reusable doc/display metadata if needed, or the repo can use BCL metadata where appropriate.

Descriptor helpers compose metadata from the packages that are referenced. Base packages should not mention optional package concepts.

## Target Rules

- Keep `ModuleDescriptor` and the builder pattern as the authoritative module metadata catalog.
- Keep runtime composition explicit. Attributes must not cause host module registration, endpoint mapping, consumer startup, task worker startup, or assembly scanning.
- Keep reflection bounded to explicit generic helper calls over known types, or replace it later with a source generator if this expands.
- Make attributes small facts, not mini configuration objects.
- Put each attribute in the package that owns and validates that fact.
- If a helper requires an attribute from its package, fail fast with a clear exception when it is missing.
- Do not put tenancy concepts in messaging/task descriptors unless a tenancy package provides that metadata feature. Generic reusable payloads should use `[ScopeAware]`.
- Do not put physical application namespace or subject prefix on event contract attributes. Subject prefixes come from application identity/configuration.
- Do not put module identity on event/handler/task attributes when the descriptor or registration context already knows the module.
- Keep permissions and cache metadata descriptor-authored unless a single local owner type emerges.
- Do not add CQRS command/query attributes unless a real descriptor/runtime consumer needs them.

## Proposed Attribute Split

Messaging:

```csharp
[IntegrationEventName(CatalogItemCreatedIntegrationEvent.EventType)]
[IntegrationEventVersion(CatalogItemCreatedIntegrationEvent.EventVersion)]
public sealed record CatalogItemCreatedIntegrationEvent(...) : IntegrationEvent
{
    public const string EventType = "item-created";
    public const int EventVersion = 1;
}

[IntegrationEventHandler("catalog-item-created-projection")]
internal sealed class CatalogItemCreatedProjectionHandler
    : IIntegrationEventHandler<CatalogItemCreatedIntegrationEvent>;
```

Tasks:

```csharp
[TaskName(RebuildCatalogItemProjectionPayload.TaskName)]
[TaskPayloadVersion(RebuildCatalogItemProjectionPayload.PayloadVersion)]
[TaskKind(ModuleTaskKind.OneShot)]
[TaskWorkerGroup("projection-workers")]
[SupportsTaskControl]
[ScopeAware]
public sealed record RebuildCatalogItemProjectionPayload(...) : ITaskPayload
{
    public const string TaskName = "rebuild-catalog-item-projections";
    public const int PayloadVersion = 1;
}
```

Scoping:

```csharp
[ScopeAware]
public sealed record SomeScopeOwnedPayload(...) : ITaskPayload;
```

The exact names can change during implementation, but the ownership boundary should not.

## Descriptor Pipeline Shape

Descriptor authoring should remain explicit:

```csharp
public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
    .Create(Name)
    .WithSchema(Schema)
    .WithPublishedEvent<CatalogItemCreatedIntegrationEvent>()
    .WithSubscription<CatalogItemCreatedIntegrationEvent>(CatalogItemCreatedProjectionHandlerName)
    .WithTask<RebuildCatalogItemProjectionPayload>()
    .Build();
```

Internally:

- `WithPublishedEvent<TEvent>()` reads only messaging-owned event metadata.
- `WithSubscription<TEvent>(producerModule, handlerName)` reads only producer event metadata plus explicit producer/handler context, unless a handler-aware overload is added in an application-facing package.
- `WithTask<TPayload>()` reads task-owned task metadata.
- tenancy-aware helpers or descriptor features enrich tenant scope only when `Gma.Framework.Tenancy` is referenced.
- runtime registration helpers such as `AddIntegrationEventHandler<TEvent,THandler>(consumerModule, producerModule)` and `AddTaskHandler<TPayload,THandler>(moduleName)` read only the attributes owned by their packages and any explicitly referenced optional packages.

## Implementation Plan

1. Audit the current uncommitted metadata-attribute draft.
2. Replace broad attributes with smaller package-owned attributes.
3. Remove tenancy fields from `Gma.Framework.Messaging` and `Gma.Framework.Tasks` descriptors unless they move into tenancy-owned metadata features.
4. Introduce tenancy-owned metadata readers/features only if descriptors need tenant-specific policy beyond generic scope metadata.
5. Update Auth, Catalog, Ordering, and TaskSamples to use the split attributes.
6. Keep module descriptors explicit and builder-authored.
7. Keep old explicit descriptor/registration overloads where they are still useful for edge cases and tests.
8. Update docs and ADR snippets to describe package-owned metadata composition.
9. Add tests proving missing required attributes throw clear exceptions.
10. Add architecture guards proving base packages do not reference unrelated optional packages or concepts.
11. Run targeted builds/tests and only run broader validation if the touched surface justifies it.

## Tests And Guards

Add or update tests for:

- messaging event identity attributes normalize event names and versions;
- messaging handler attributes normalize stable handler names;
- task payload identity attributes normalize task names and payload versions;
- task kind, worker group, and control metadata are independent from task payload identity where practical;
- tenancy metadata is read from tenancy-owned attributes only;
- descriptor helpers throw clear missing-attribute exceptions;
- descriptor metadata and runtime registrations still match;
- module descriptors do not silently drift from local attributes;
- `Gma.Framework.Messaging` does not reference `Gma.Framework.Tenancy`;
- `Gma.Framework.Tasks` does not reference `Gma.Framework.Tenancy` unless there is a deliberate ADR and narrow package split;
- no broad assembly scanning or Scrutor-style package dependency is introduced;
- docs index and local links remain valid.

## Out Of Scope

- Do not add external metadata/scanning libraries.
- Do not add source generators unless the attribute pipeline expands enough to justify them.
- Do not redesign the whole module descriptor builder.
- Do not introduce command/query attributes without a concrete descriptor or runtime use case.
- Do not add host auto-discovery.
- Do not change persistence migrations unless metadata changes force a model change, which they should not.

## Completion Criteria

- Attributes are split into small package-owned facts.
- Unrelated optional concepts are removed from base descriptors and attributes.
- Packages can add descriptor-pipeline pieces without forcing every other package to reference them.
- Required package attributes fail fast when missing.
- Auth, Catalog, Ordering, and TaskSamples compile with the new metadata shape.
- Architecture tests enforce package boundaries and no hidden composition magic.
- Docs explain how to add new metadata attributes and descriptor pipeline pieces safely.
