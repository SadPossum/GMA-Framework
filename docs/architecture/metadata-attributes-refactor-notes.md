# Metadata Attributes Refactor Notes

Temporary notes for the local-metadata attribute refactor. Keep until the slice stabilizes, then fold the durable guidance into module-system, messaging, and task docs.

## Direction

- Keep `ModuleDescriptor` and the explicit builder pattern as the authoritative module catalog.
- Use attributes only when metadata naturally belongs to a local type:
  - published integration-event identity belongs on the integration event contract type;
  - integration-event consumer handler identity belongs on the handler type;
  - task identity belongs on the serialized task payload contract type.
- Keep host/module composition explicit. Attributes do not scan assemblies, register modules, start consumers, map endpoints, or enable tasks by themselves.
- Keep reflection bounded to explicit generic helper calls such as `WithPublishedEvent<TEvent>()`, `WithTask<TPayload>()`, `AddIntegrationEventHandler<TEvent,THandler>(consumerModule, producerModule)`, and `AddTaskHandler<TPayload,THandler>(moduleName)`.
- Leave permissions and cache metadata descriptor-authored for now. Permissions span public code constants plus typed admin wrappers, and cache metadata spans helper usage rather than one executable type.
- Leave CQRS command/query metadata alone for now. Commands do not currently feed module descriptors directly; adding a command attribute without a real catalog/authorization/runtime consumer would create decoration without architectural value.

## External Check

- .NET attributes are metadata that code reads through reflection; they do not do work by themselves.
- .NET trimming guidance prefers avoiding broad reflection, or keeping reflection narrow and analyzable when it is useful.
- A source generator could remove runtime reflection later, but it is heavier than this milestone needs. If attribute usage expands beyond explicit generic helper calls, revisit incremental source generation.

## Audit While Implementing

- Descriptor metadata and runtime registrations must still match through architecture tests.
- Contract projects must not depend on application projects just to read handler attributes.
- Task payloads that are serialized across the task runtime should live in `.Contracts` when their identity is descriptor metadata.
- Missing attributes should fail fast with clear messages from the helper being used.
