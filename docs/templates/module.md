# <Module> Module

## Purpose

Describe what this module owns and what it intentionally does not own.

## Projects

```text
<Module>.Contracts
<Module>.Domain
<Module>.Application
<Module>.Infrastructure
<Module>.Persistence
<Module>.Persistence.SqlServerMigrations
<Module>.Persistence.PostgreSqlMigrations
<Module>.Api
<Module>.Admin.Contracts
<Module>.AdminCli
<Module>.AdminApi
```

Remove projects that do not exist.

## Public Contracts

List public request/response DTOs, enums, admin permission code strings, module metadata, integration events, and public user notification payloads.
Keep one public contract type per file in `<Module>.Contracts`.
Place public contract files in the standard folders:

- `Api/` for public request/response/DTO records.
- `Admin/` for admin-facing DTO records that remain backend-free.
- `Events/` for integration event payloads and subject constants.
- `Exports/` for producer-owned projection rebuild/backfill snapshots and source ports.
- `Metadata/` for `<Module>ModuleMetadata`, `*PermissionCodes`, `*Profile`/`*Profiles`, `*CompositionFeatures`, and `*ContractLimits`.
- `Serialization/` for owner-package JSON converters for public contract enums and other stable wire types.
- `Types/` for public enum-like or code-list types.

Confirm the public contracts `.csproj` references `Gma.Framework.Modules` for module metadata, `Gma.Framework.ModuleComposition` for public profiles/composition features, `Gma.Framework.Authorization` for permission metadata, `Gma.Framework.Messaging` for integration events/subscriptions, `Gma.Framework.Caching` for cache metadata, and `Gma.Framework.Tasks` for task metadata/contracts only when those capabilities are declared. Public contracts should avoid `Gma.Framework.Application.Composition` and `Gma.Framework.Cqrs`; keep CQRS commands/queries and paging helpers in the module application boundary. Optional producer `.Contracts` references are allowed. Keep package and framework references out.
If the module exposes rebuild/backfill export contracts, reference `Gma.Framework.ProjectionRebuild` from `.Contracts`, keep the source interface backend-free, and implement the source in the producer persistence adapter.
Admin permission code strings live here so `<Module>ModuleMetadata` can declare permissions without referencing admin-only framework packages.
When the module becomes compiled code, add its contract metadata descriptor to `tests/Architecture.Tests/Support/ArchitectureCatalog.cs`; architecture tests compare that catalog with every `<Module>ModuleMetadata.Descriptor`.
If this module consumes another module's contracts, do not expose producer DTOs or enums from this module's public contracts. Duplicate the scalar/read-model fields owned by this module.

## Admin Contracts

List typed `AdminPermission` constants in `<Module>.Admin.Contracts`, if the module has admin CLI or admin API surfaces. These typed wrappers should point at the code strings from `<Module>.Contracts`. Confirm that public `<Module>.Contracts` does not reference `Gma.Framework.Administration`.
Place typed permission wrappers in `Permissions/` and operation-name constants in `Operations/`.
Confirm the admin contracts `.csproj` references only `Gma.Framework.Administration` and the owning public `<Module>.Contracts` project, with no package or framework references.

The module scaffolder seeds admin-capable modules with one `<module>.manage` permission in public metadata and typed admin contracts through `ModuleDescriptor.WithPermission(...)`. Replace or split that seed permission once the module has real resources and operations; keep `*PermissionCodes` constants and descriptor metadata in sync whether the final module uses single-item or bulk helpers.

## Endpoints

Base path:

```text
/api/<module>
```

Endpoints:

- `METHOD /path`

State whether endpoints require tenant context, authorization, or both.
Confirm the public API `.csproj` has no package references, only an optional `Microsoft.AspNetCore.App` framework reference, shared API/application references as needed, and owning module contracts/application/persistence/infrastructure adapters.

If the module has selectable profiles, expose explicit front-door overloads such as `Add<Module>Module(<Module>Profile profile)`. The overload may call `SelectModuleProfile(...)`, but it must still require the host to compose the module intentionally. Do not select profiles through assembly scanning or configuration-only magic.

## Admin Commands

List optional CLI commands, required permissions, tenant requirements, destructive confirmation, and secret-handling rules.
Confirm the admin CLI `.csproj` uses `System.CommandLine` as its only package reference, has no framework references, and references only shared administration/application contracts as needed plus owning module contracts/application/persistence/infrastructure adapters.

| Command | Permission | Tenant required | Destructive |
| --- | --- | --- | --- |
| `<module> <resource> <action>` | `<module>.<resource>.<action>` | yes/no | yes/no |

## Admin API

List optional admin HTTP routes, required permissions, tenant requirements, destructive confirmation, and secret-handling rules.
Confirm the admin API `.csproj` has no package references, only an optional `Microsoft.AspNetCore.App` framework reference, shared API/admin API/application references as needed, and owning module admin contracts/contracts/application/persistence/infrastructure adapters.

| Method | Route | Permission | Tenant required | Destructive |
| --- | --- | --- | --- | --- |
| `GET` | `/api/admin/<module>/<resource>` | `<module>.<resource>.read` | yes/no | no |

## Domain Model

List aggregates, entities, value objects, domain events, and core invariants.
Confirm domain events inherit `DomainEvent` or `TenantDomainEvent` from `Gma.Framework.Domain` so event id, occurrence time, and tenant id rules stay centralized.
Confirm the domain project references only shared domain/error primitives and does not reference contracts, application, persistence, infrastructure, HTTP, admin, EF, or host abstractions.
Confirm the domain `.csproj` has no package or framework references unless a future ADR explicitly expands the domain dependency model.

## Application Layer

List commands, queries, handlers, validators, domain event handlers, and integration event handlers.
State which commands implement `ITransactionalCommand<TResponse>` and which commands intentionally remain plain `ICommand<TResponse>`.
List task payloads and daemons, if any. State the `TaskNameAttribute` value, `TaskPayloadVersionAttribute` value, `TaskKindAttribute`, tenant scope marker, worker group, cancellation behavior, and whether control messages are supported. Keep serialized task payload contracts in `.Contracts` when they appear in module metadata, expose task identity constants on the payload type, then declare them through `ModuleDescriptor.Create(...).WithTask<TPayload>().Build()`.
If the module owns a projection rebuild task, state the projection name/version, source contract, writer, checkpoint store, cursor semantics, dry-run behavior, and whether retry resumes from the same run id. Prefer `Gma.Framework.ProjectionRebuild` for the task-neutral loop and `Gma.Framework.ProjectionRebuild.Tasks` only when adapting that loop to task progress/control. EF-backed persistence projects may reference `Gma.Framework.ProjectionRebuild.EntityFrameworkCore` for checkpoint state/store/mapping helpers, but contracts should reference only the backend-free `Gma.Framework.ProjectionRebuild` package when they expose rebuild/export contracts.
Keep one handler class per file under `<Module>.Application/Handlers`, including command handlers, query handlers, domain-event projectors, and integration-event handlers.
Confirm the application project does not reference module adapters or front doors such as `.Persistence`, `.Infrastructure`, `.Api`, `.AdminCli`, or `.AdminApi`.
Confirm the application `.csproj` references only needed shared abstractions such as `Gma.Framework.Cqrs`, `Gma.Framework.Application.Composition` for assembly registration, `Gma.Framework.Application.Events` for domain-event handlers, and `Gma.Framework.Pagination` for normalized paging, plus its own contracts/domain projects, optional producer `.Contracts` projects, and small Microsoft extension abstraction packages.
Do not reference `Gma.Framework.Administration` from feature module application projects; keep admin framework usage in `.AdminCli`/`.AdminApi` front doors. `Gma.Modules.Administration.Application` is the owner-side exception.
Confirm that handlers use `ISystemClock` and `IIdGenerator` instead of direct system time or ID generation.
Confirm that application DI registration extends `IServiceCollection`, not `IHostApplicationBuilder`.
Confirm that application DI extension methods reject null receivers and call `AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly)` for CQRS handlers, validators, and domain-event handlers.
Confirm that published integration-event contracts expose `EventType`/`EventVersion` constants, use `IntegrationEventNameAttribute` and `IntegrationEventVersionAttribute`, tenant-owned events add `[TenantScoped]`, consumer handlers use `IntegrationEventHandlerAttribute`, and handlers are registered explicitly with `AddIntegrationEventHandler<TEvent,THandler>(consumerModule, producerModule)`.
Confirm that task payload contracts use split task attributes, handlers are registered explicitly with `AddTaskHandler<TPayload,THandler>(moduleName)`, and attributes match the module's descriptor metadata, including kind, tenant scope, payload version, worker group, and control-message support.
Confirm that task payloads depend only on `Gma.Framework.Tasks`, `Gma.Framework.Tasks.Cqrs` when they dispatch application commands, `Gma.Framework.ProjectionRebuild.Tasks` when they adapt rebuilds to task progress/control, CQRS contracts, and module ports; they must not depend on scheduler packages, HTTP, CLI, or another module's internals. Long-running task payloads should poll `ITaskControlLoop` or use `TaskControlLoopExtensions` at safe checkpoints, mark acted-on control messages handled or failed, and throw `TaskRunCanceledException` for cooperative cancel/drain stops.
If the module has recurring work, list each `ITaskScheduleProvider` schedule and its interval, tenant behavior, payload version, and dedupe key strategy. Schedules should enqueue task requests only. Prefer the default version-aware dedupe shape `schedule:<module>:<task>:<schedule>:v<payload-version>:<occurrence>` unless the module documents a safer custom key.
If the module exposes task admin filters or docs, use `TaskRunStatusNames` wire names such as `retry-scheduled` and `cancellation-requested` rather than raw enum names.

## Infrastructure

Describe adapters and external systems.

## Persistence

Describe schema, DbContext, migrations, repositories, local projections, and outbox/inbox tables.
State the module name used by `IUnitOfWork`, `IOutboxWriter`, `IOutboxStore`, and `IInboxStore`.
For tenant-owned persisted models, state whether each type inherits `TenantAggregateRoot<TId>`, inherits `TenantEntity<TId>`, or implements `ITenantScoped` directly. EF-backed tenant-aware modules should inherit `TenantAwareDbContext<TContext>` and call `ApplyTenantConventions(modelBuilder)` after module configurations are applied. Keep tenant-local uniqueness and read-path indexes in module configurations.
Do not add shadow tenant columns through host scanning or broad reflection. The only tenant convention reflection allowed by default is inside shared EF helpers over the current module `ModelBuilder`.
If the module owns rebuildable projections, list the checkpoint table and key shape. Tenant-scoped rebuild checkpoints should include tenant id, projection name, run id, cursor, processed/written/skipped/failed counts, projection version, updated timestamp, and completion timestamp.
For EF-backed checkpoint tables, prefer a concrete module checkpoint type inheriting `ProjectionRebuildCheckpointState`, `ConfigureProjectionRebuildCheckpointState(...)` in the entity configuration, and a thin module store inheriting `EfProjectionRebuildCheckpointStore<TDbContext,TCheckpointState>`.
If the rebuild writer and checkpoint store share one DbContext, register a thin module transaction boundary over `EfProjectionRebuildTransactionBoundary<TDbContext>` so each batch and checkpoint save commit atomically. Do not register a boundary when the writer touches external systems or separate stores that cannot share the transaction.
For EF-backed modules that raise domain events, confirm the module UoW inherits `EfDomainEventUnitOfWork<TDbContext>` instead of duplicating domain-event dispatch/save/clear logic.
Confirm the persistence `.csproj` stays a provider adapter: EF SQL Server/PostgreSQL plus hosting packages only, shared application/domain/infrastructure primitives, the owning contracts/application/domain projects, and optional producer `.Contracts` projects for local projections.
Confirm inbox/outbox EF configurations call `ConfigureInboxMessage(...)` and `ConfigureOutboxMessage(...)` from `Gma.Framework.Messaging.Infrastructure` instead of repeating message keys, indexes, and length limits.
If the module has a persistence project, keep both SQL Server and PostgreSQL migration projects unless a future ADR explicitly narrows provider support.
Confirm that EF design-time factories and `Microsoft.EntityFrameworkCore.Design` live only in provider-specific migration projects.
Confirm that provider-specific migration projects reference only the owning `<Module>.Persistence` project.
Confirm that persistence DI extension methods reject null receivers, call `AddPersistenceOptions(builder.Configuration)`, use `TryAddModuleDbContext`, and register UoW/outbox/inbox services through `TryAddEnumerable`.
Confirm that persisted enum numeric values are stable, public contract/domain-state enums use `Unknown = 0`, and public contract enums have owning-package wire-name helpers plus `[JsonConverter]` tests.

## Integration Events

| Event | Subject | Version | Tenant-scoped |
| --- | --- | --- | --- |
| `<EventName>` | `{application-namespace}.<module>.<event>.v1` | `1` | yes/no |

Confirm public integration event contracts inherit `IntegrationEvent` from `Gma.Framework.Messaging`, pass the stable event name/version to the base constructor, and keep only payload-specific validation in the module contract type. Tenant-owned events should inherit `TenantIntegrationEvent` from `Gma.Framework.Tenancy.Messaging`, and hosts that publish or consume them should compose `AddTenantAwareMessaging()`.
Confirm subject constants or accessors render through `IntegrationEventNaming` or module subject factory methods, with `gma` only as the default `ApplicationIdentity:Namespace`.

## Notifications

List user-facing notification payloads or durable notification request events, names, versions, intended recipients, tenant scope, and whether delivery is best-effort only or backed by another durable fact.

| Notification | Name | Version | Recipient | Durable source |
| --- | --- | --- | --- | --- |
| `CatalogItemUpdatedNotification` | `catalog.item-updated` | `1` | tenant user | `UpdateCatalogItemCommand` enqueues request, CQRS bridge flushes after commit |

For best-effort live delivery, confirm notification payloads implement `IUserNotificationPayload`, use `NotificationNameAttribute`, `NotificationVersionAttribute`, and `NotificationDescriptionAttribute`, and are declared in module metadata through `ModuleDescriptor.Create(...).WithUserNotification<TPayload>().Build()`. Transactional command handlers may enqueue through `IUserNotificationRequestQueue`; front doors/workers may publish through `IUserNotificationPublisher` only after committed state is safe to expose. Module code should reference only `Gma.Framework.Notifications`; `Gma.Framework.Notifications.Cqrs`, `Gma.Framework.Realtime.Notifications`, SSE, SignalR, and ASP.NET streaming adapters belong to host/front-door composition.

For guaranteed notification history, publish `UserNotificationRequestedIntegrationEvent` from `Gma.Modules.Notifications.Contracts` through the producing module's own outbox. The producing module may reference `Gma.Modules.Notifications.Contracts` but must not reference `Gma.Modules.Notifications.Application`, `Gma.Modules.Notifications.Domain`, `Gma.Modules.Notifications.Persistence`, `Gma.Modules.Notifications.Api`, or `Gma.Modules.Notifications.AdminApi`. Add an explicit producer subscription in the Notifications module/host composition, for example `AddUserNotificationRequestSubscription(<ProducerModuleMetadata>.Name)`; do not write notification history directly.

Do not put passwords, access tokens, refresh tokens, token hashes, raw secrets, tenant-private authorization decisions, or large payload blobs in notifications.

## Inbound Subscriptions

List producer contracts consumed by this module. State handler name, subject, local projection, idempotency key, and whether tenant context is set from the event.

| Producer | Event | Subject | Handler | Local state updated |
| --- | --- | --- | --- | --- |
| `<Producer>` | `<EventName>` | `{application-namespace}.<producer>.<event>.v1` | `<stable-handler-name>` | `<projection/table>` |

Use lowercase kebab-case for application namespace, module, event, and handler-name segments. Subjects must follow `{application-namespace}.<module>.<event>.v<version>`.
Confirm that the module references producer `.Contracts` only.
Confirm that consumed producer enum/status values are validated before they affect local decisions.

## Tests

List unit, architecture, and integration test coverage.

## Observability

List module meters, instruments, activity sources, structured log properties, and any alert-worthy conditions. Confirm that metric tags are bounded.

## Caching

List explicit cache-aside reads, logical keys, tags, TTL policy, and the commands/domain events that enqueue invalidation. State why each cached value is non-authoritative and whether it is tenant or global scoped. Confirm that the module references only `Gma.Framework.Caching` caching contracts. Tenant-owned cache keys should require `CachingCompositionFeatures.TenantScopeRequired(...)`; hosts satisfy that through `Gma.Framework.Tenancy.Caching`.

## Extension Points

List likely future changes and the intended extension mechanism.

## Composition Profiles

List available module profiles, provided features, required features, required modules, and the host calls that select each profile.

| Profile | Provides | Requires | Required modules |
| --- | --- | --- | --- |
| `default` | `<module>.<feature>` | `<capability>.<feature>` | `<other-module>` |

Confirm that `ModuleDescriptor.Create(...).WithProfile(...)` documents the profiles in `<Module>.Contracts`, host/front-door composition calls `SelectModuleProfile(...)`, shared adapters call `ProvideFeature(...)` for generic capabilities, and every runtime host calls `ValidateModuleComposition()` after explicit modules/adapters are added and before serving traffic or executing commands.
