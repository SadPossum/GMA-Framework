# Shared Dependency Boundary Refactor Notes

Temporary working notes for tightening shared-package dependencies. Keep this file while the dependency-boundary refactor is active, then fold the durable decisions into the architecture overview, module system docs, and ADRs.

## Direction

- Optional adapters should depend on the smallest contract package that describes their composition seam.
- Runtime packages can compose the narrower runtime slices they need, but those dependencies should stay explicit in package names and architecture guards.
- Capability cores should own metadata, public value types, and adapter markers only when that keeps optional backends from referencing heavier runtime packages.
- Do not split a new package only for neatness. Add a project when there is a concrete second consumer or when an existing dependency would drag unrelated runtime concerns.

## Current Slice

- `Gma.Framework.Caching.Redis` previously referenced `Gma.Framework.Caching.Infrastructure` only to read `CachingOptions`, `CacheProvider`, and `IDistributedCacheAdapterRegistration`.
- Those tiny provider/adapter seam types now live in `Gma.Framework.Caching`, so Redis depends on cache contracts rather than the HybridCache runtime package.
- `Gma.Framework.Caching.Infrastructure` still owns option validation, HybridCache registration, metrics, physical key formatting, and fail-open runtime behavior, while tenant scope value resolution lives in `Gma.Framework.Tenancy.Caching`.
- `Gma.Framework.Caching.Cqrs` now owns the command invalidation pipeline behavior and composes cache infrastructure plus CQRS infrastructure explicitly.
- The shared dependency manifest and architecture overview now encode `Gma.Framework.Caching.Redis -> Gma.Framework.Caching`, keep CQRS references in `Gma.Framework.Caching.Cqrs`, and keep tenancy references in `Gma.Framework.Tenancy.Caching`.
- `ICacheInvalidationQueueFlusher` is internal again; `Gma.Framework.Caching.Infrastructure` grants `InternalsVisibleTo("Gma.Framework.Caching.Cqrs")` so the bridge can flush deferred invalidations without widening the public cache API.
- `Gma.Framework.Tasks.Cqrs` now owns `AddTaskCqrs()` and the `ITaskCommandDispatcher` implementation; `Gma.Framework.Tasks.Infrastructure` no longer composes CQRS or registers task command dispatch.
- `Gma.Framework.ProjectionRebuild` is task-neutral again. `Gma.Framework.ProjectionRebuild.Tasks` adapts rebuild observers to `ITaskRuntimeReporter` and `ITaskControlLoop` only for task-backed callers.
- `Gma.Framework.Api.Serilog` is tenant-neutral again. `Gma.Framework.Tenancy.Api.Serilog` contributes tenant id request-log enrichment only when hosts explicitly compose the bridge.

## Follow-Up Audit Targets

- Keep `Gma.Framework.Naming` focused on identifier syntax and avoid using it as a generic utility bucket.
