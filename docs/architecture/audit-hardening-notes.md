# Architecture Hardening Notes

These are current audit notes for future refactoring passes. They are intentionally practical rather than aspirational.

## Fixed In This Pass

- Commands that write state now implement `ITransactionalCommand<TResponse>`.
- The command UoW behavior commits exactly one module-owned `IUnitOfWork`.
- Application handlers resolve outbox writers through `IOutboxWriterRegistry`.
- Inbox failure handling rolls back handler side effects before recording failure metadata.
- Outbox publishing starts only when a host opts into a concrete messaging adapter.
- Disabled NATS consumers can be registered without a NATS connection; the connection is resolved only after consumer runtime is enabled.
- NATS pull fetch expiration is clamped above the client minimum, while polling/retry delay remains configurable.
- NATS stream names, subjects, durable prefixes, cache key prefixes, and app-owned meter names derive from `ApplicationIdentity:Namespace` unless an adapter-specific physical override is configured.
- Architecture tests now guard production project package references so `System.CommandLine` stays in CLI front doors and backend adapter packages stay out of module projects.
- Repeated architecture-test module assembly lists were replaced with one explicit test-only `ArchitectureCatalog`.
- Feature-module typed admin permission constants moved into optional `<Module>.Admin.Contracts` projects, leaving public `.Contracts` free of `Gma.Framework.Administration`.

## Keep Watching

- Design-time EF factories and provider migration project files are repetitive. A shared design-time helper would reduce boilerplate once the module examples settle.
- Result primitives now live in `Gma.Framework.Results`; keep this package dependency-free and avoid folding unrelated shared-kernel concepts into it.

## Reflection And Magic Boundary

Runtime host composition remains explicit. Reflection is acceptable for dispatch, diagnostics, or architecture tests when it is documented and covered by tests. New reflection-based behavior should not silently register modules, handlers, persistence, or transports by default.

Allowed production reflection is currently limited to:

- `ApplicationServiceCollectionExtensions`, which scans one explicitly supplied module application assembly for CQRS handlers, validators, and domain-event handlers.
- `RequestDispatcher`, which compiles cached typed delegates for runtime command/query instances.
- `DomainEventDispatcher`, which compiles cached typed delegates for runtime domain-event instances.
- `IntegrationEventHandlerInvoker`, which compiles cached typed delegates for integration-event consumers.
- `TaskHandlerInvoker`, which deserializes registered task payloads and compiles cached typed delegates for task handlers.
- `ModuleMetadataAttributeReader`, which reads attributes from explicitly supplied generic event/task/payload types and collects optional package-owned metadata contributors.
- `TenantEntityTypeBuilderExtensions`, which applies tenant query filters and tenant/global markers during EF model building.
- EF `ApplyConfigurationsFromAssembly` inside module-owned `DbContext` classes.
- host assembly marker classes used by test hosts and architecture tests.
- observability module-name inference from assembly names.

Do not use reflection or attributes to auto-register modules, handlers, persistence stores, admin surfaces, messaging transports, or cache adapters without a separate ADR plus architecture tests proving the default host composition remains explicit.

`ArchitectureCatalog` is a test/tooling catalog only. It centralizes architecture-test inputs, but it must not be used by runtime hosts to compose modules.
