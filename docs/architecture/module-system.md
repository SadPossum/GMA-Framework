# Module System

The module system is intentionally small.

```csharp
public interface IModule
{
    string Name { get; }
    void AddServices(IHostApplicationBuilder builder);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

## Rules

- Modules are registered explicitly in `Host.Api`.
- Modules are not discovered by assembly scanning.
- A module owns its endpoints, application use cases, domain model, persistence, and contracts.
- A module should expose only contracts and integration events to other modules.
- A module must not require another module's internals.
- Persistent module commands implement `ITransactionalCommand<TResponse>`.
- Module `IUnitOfWork` and `IOutboxWriter` implementations declare `ModuleName`; shared infrastructure refuses ambiguous matches.
- Module projects must not reference concrete transport, cache-backend, or observability-exporter packages directly. Use shared abstractions plus the host-selected adapter.

## Shared Project Layers

The shared core is intentionally small:

- `Gma.Framework.Naming`, `Gma.Framework.Numerics`, `Gma.Framework.Observability`, and `Gma.Framework.Results` stay dependency-free.
- `Gma.Framework.Domain` owns aggregate/domain-event primitives and depends only on `Gma.Framework.Naming` for shared identifier syntax such as tenant ids and `Gma.Framework.Numerics` for reusable numeric validation.
- `Gma.Framework.Modules` owns module metadata primitives and references only `Gma.Framework.Naming`.
- `Gma.Framework.ModuleComposition` owns module profile, provided-feature, required-feature, required-module, and fail-fast composition validation primitives. It references only `Gma.Framework.Modules`, `Gma.Framework.Naming`, and hosting abstractions needed by composition roots.
- `Gma.Framework.Permissions` owns permission metadata descriptor extensions and references only `Gma.Framework.Modules` and `Gma.Framework.Naming`.
- `Gma.Framework.Caching` owns cache contracts, provider/options seams, adapter markers, cache descriptor metadata, and cache composition feature ids. It references `Gma.Framework.ModuleComposition`, `Gma.Framework.Modules`, and `Gma.Framework.Naming`.
- `Gma.Framework.Messaging` owns integration event, outbox/inbox, subscription, messaging descriptor contracts, and messaging composition feature ids. It references shared primitives plus DI abstractions, not transport adapters.
- `Gma.Framework.Tasks` owns task contracts, task descriptor metadata, and task runtime composition feature ids. It does not reference CQRS or runtime adapters.
- `Gma.Framework.Caching.Cqrs` owns the optional bridge for flushing deferred cache invalidations after successful CQRS unit-of-work commits.
- `Gma.Framework.Tasks.Cqrs` owns the optional bridge for dispatching application commands from task payload handlers.
- `Gma.Framework.ProjectionRebuild` owns the task-neutral rebuild loop, checkpoint contracts, source/writer contracts, and metrics; `Gma.Framework.ProjectionRebuild.Tasks` adapts that loop to task progress and control messages.
- `Gma.Framework.Runtime` owns clock/id abstractions and dependency-free runtime naming helpers.
- `Gma.Framework.Tenancy` owns tenant context contracts, tenant options, and tenant errors.
- `Gma.Framework.Security` owns dependency-free claim/security constants and authentication-assurance requirements shared by HTTP adapters and token issuers.
- `Gma.Framework.Security.AspNetCore` optionally enforces authentication context and freshness requirements through RFC 9470 HTTP challenges without depending on Auth.
- `Gma.Framework.Cqrs` owns command/query contracts, validators, dispatcher contracts, `Unit`, and transactional unit-of-work contracts.
- `Gma.Framework.Application.Events` owns domain-event handler and dispatcher contracts. It references `Gma.Framework.Domain` only.
- `Gma.Framework.Pagination` owns normalized paging request helpers and remains dependency-free.
- `Gma.Framework.Application.Composition` owns constrained application assembly registration only. It may reference `Gma.Framework.Application.Events`, `Gma.Framework.Cqrs`, and small dependency-injection abstractions, but not domain models directly, HTTP, EF, messaging transports, cache backends, logging backends, hosting, or provider packages.
- Adapter projects such as `Gma.Framework.Infrastructure`, `Gma.Framework.Application.Events.Infrastructure`, `Gma.Framework.Cqrs.Infrastructure`, `Gma.Framework.Runtime.Infrastructure`, `Gma.Framework.Tenancy.Infrastructure`, `Gma.Framework.Tenancy.Api.Serilog`, `Gma.Framework.Tenancy.Caching`, `Gma.Framework.Tenancy.Cqrs`, `Gma.Framework.Tenancy.Tasks`, `Gma.Framework.Caching.Infrastructure`, `Gma.Framework.Caching.Cqrs`, `Gma.Framework.Messaging.Infrastructure`, `Gma.Framework.Messaging.Nats`, `Gma.Framework.Tasks.Infrastructure`, `Gma.Framework.ProjectionRebuild.Tasks`, `Gma.Framework.Persistence.EntityFrameworkCore`, `Gma.Framework.Api.*`, `Gma.Framework.Caching.Redis`, `Gma.Framework.Messaging.Nats.Aspire`, and `Gma.Framework.Logging.Serilog` own concrete runtime packages.

This keeps every module free to depend on shared contracts and primitives without inheriting optional infrastructure choices.

Shared project ownership quick reference:

- `Gma.Framework.Infrastructure`: host-level facade that composes the baseline runtime adapters below.
- `Gma.Framework.Application.Events.Infrastructure`: domain-event dispatcher implementation.
- `Gma.Framework.Cqrs.Infrastructure`: request dispatcher, CQRS pipeline behaviors, command unit-of-work behavior, and CQRS runtime registration.
- `Gma.Framework.Runtime.Infrastructure`: default clock and id generator implementations.
- `Gma.Framework.Tenancy.Infrastructure`: default/null tenant context, tenant option validation, and baseline tenancy service wiring.
- `Gma.Framework.Tenancy.Api.Serilog`: optional tenant-to-HTTP-request-log bridge that contributes tenant id to Serilog diagnostic context without making the base Serilog adapter depend on tenancy.
- `Gma.Framework.Tenancy.Caching`: optional tenant-to-cache bridge that resolves tenant-owned cache scope values without making cache infrastructure depend on tenancy.
- `Gma.Framework.Tenancy.Cqrs`: optional tenant-to-CQRS logging bridge that contributes tenant context to CQRS log scopes without making CQRS infrastructure depend on tenancy.
- `Gma.Framework.Tenancy.Tasks`: optional tenant-to-task execution bridge that prepares tenant context for scope-aware task handlers without making task infrastructure depend on tenancy.
- `Gma.Framework.Caching.Infrastructure`: HybridCache-backed cache-aside runtime, cache invalidation queue, cache metrics, and cache option validation.
- `Gma.Framework.Caching.Cqrs`: optional command pipeline bridge that flushes deferred cache invalidations after successful CQRS unit-of-work commits.
- `Gma.Framework.Messaging.Infrastructure`: EF outbox/inbox base helpers, outbox publisher, outbox options, a null event bus, and messaging metrics.
- `Gma.Framework.Messaging.Nats`: NATS JetStream publisher/consumer runtime, NATS options, and low-level NATS composition hooks.
- `Gma.Framework.Tasks.Infrastructure`: EF task-run store base, task worker/scheduler hosted services, task control loop, task options, and task metrics.
- `Gma.Framework.Tasks.Cqrs`: optional task-to-CQRS command dispatcher contract and runtime registration. Hosts compose `AddTaskCqrs()` only when task payload handlers dispatch application commands.
- `Gma.Framework.ProjectionRebuild`: task-neutral rebuild runner, source/writer contracts, checkpoint contracts, bounded metrics, and default no-op observer.
- `Gma.Framework.ProjectionRebuild.Tasks`: optional task adapter that maps rebuild progress/control polling to `ITaskRuntimeReporter` and `ITaskControlLoop`.
- `Gma.Framework.Persistence.EntityFrameworkCore`: EF provider selection, design-time DbContext options, persistence options, and domain-event unit-of-work base.
- `Gma.Framework.Observability.Infrastructure`: shared CQRS metric implementations, module-name resolution, and bounded tag normalization. Capability metrics live beside their owning runtime adapters.
- `Gma.Framework.Modules`: module descriptor, descriptor builder, descriptor feature base, generic metadata naming/guard helpers, and custom metadata feature support.
- `Gma.Framework.ModuleComposition`: profile metadata, composition feature requirements/providers, module requirement validation, and host-level validation extensions.
- `Gma.Framework.Permissions`: permission metadata and `WithPermission(...)` / `WithPermissions(...)` / `GetPermissions()` descriptor extensions.
- `Gma.Framework.Cqrs`: command/query contracts, validators, dispatcher contracts, `Unit`, and transactional unit-of-work contracts.
- `Gma.Framework.Cqrs.Infrastructure`: CQRS dispatcher and pipeline behavior implementations.
- `Gma.Framework.Application.Events`: domain-event handler and dispatcher contracts.
- `Gma.Framework.Application.Events.Infrastructure`: domain-event dispatcher implementation.
- `Gma.Framework.Application.Composition`: constrained application assembly registration.
- `Gma.Framework.Naming`: low-level shared naming and identifier syntax primitives.
- `Gma.Framework.Numerics`: dependency-free numeric validation helpers shared by domain and contract metadata.
- `Gma.Framework.Observability`: vendor-neutral metric, log-property, and tag names.
- `Gma.Framework.Pagination`: normalized paging request helpers.
- `Gma.Framework.Runtime`: shared runtime abstractions and dependency-free runtime helpers such as clock/id generator contracts and worker-id normalization.
- `Gma.Framework.Runtime.Infrastructure`: default runtime implementations for clock and id generator contracts.
- `Gma.Framework.Security`: shared claim/security constants and authentication-assurance requirements.
- `Gma.Framework.Security.AspNetCore`: optional authentication-assurance endpoint enforcement.
- `Gma.Framework.Caching`: cache-aside contracts, cache key/tag primitives, provider/options contracts, distributed adapter registration marker, and cache descriptor metadata.
- `Gma.Framework.Tenancy.Caching`: optional tenant-to-cache scope bridge.
- `Gma.Framework.Tenancy.Tasks`: optional tenant-to-task execution context bridge.
- `Gma.Framework.Caching.Cqrs`: optional cache-to-CQRS invalidation bridge.
- `Gma.Framework.Messaging`: integration event contracts, outbox/inbox contracts, subscription registry contracts, and messaging descriptor metadata.
- `Gma.Framework.Tasks`: task payload, handler, control, schedule, run-store, and task descriptor contracts.
- `Gma.Framework.Tasks.Cqrs`: optional task-to-CQRS command dispatcher contract and bridge.
- `Gma.Framework.Tenancy`: tenant context contracts, tenant options, and tenant errors.
- `Gma.Framework.Api`: ASP.NET Core-neutral API primitives and endpoint helpers.
- `Gma.Framework.Api.OpenApi`: Swagger/OpenAPI package ownership.
- `Gma.Framework.Api.Serilog`: tenant-neutral HTTP request logging enrichment package ownership.
- `Gma.Framework.Tenancy.Api.Serilog`: optional tenant-to-request-logging enrichment bridge.
- `Gma.Framework.Logging.Serilog`: host logging configuration package ownership.
- `Gma.Framework.Caching.Redis`: Redis cache adapter package ownership. It depends only on `Gma.Framework.Caching` contracts plus Redis packages, not the HybridCache runtime package.
- `Gma.Framework.Messaging.Nats.Aspire`: Aspire/NATS client composition package ownership.
- `Gma.Framework.Administration`: backend-agnostic administrative execution, authorization, and audit-write contracts.
- `Gma.Framework.Administration.Cli`: System.CommandLine administration front-door helpers.
- `Gma.Framework.Administration.Api`: administration HTTP front-door helpers.

## Module Projects

Recommended projects:

- `<Module>.Contracts`
- `<Module>.Domain`
- `<Module>.Application`
- `<Module>.Infrastructure`
- `<Module>.Persistence`
- `<Module>.Persistence.SqlServerMigrations`
- `<Module>.Persistence.PostgreSqlMigrations`
- `<Module>.Api`
- `<Module>.Admin.Contracts`
- `<Module>.AdminCli`
- `<Module>.AdminApi`

Not every module needs every project. Keep small modules small.

## Contracts

`<Module>.Contracts` contains DTOs and integration events that other modules or clients may use.

Contract projects use a stable physical folder taxonomy:

- `Api/` for normal public request/response/DTO contracts.
- `Admin/` for admin-facing DTO contracts that must remain backend-free but are used by admin CLI/API flows.
- `Events/` for integration event payloads and subject constants.
- `Metadata/` for module descriptors, permission code strings, contract limits, and other tooling-visible metadata.
- `Types/` for public enum-like or code-list contract types.

The physical folders are for discoverability and architecture tests. File namespaces currently remain `<Module>.Contracts` unless a later deliberate breaking change moves to subnamespaces.

Allowed examples:

- request/response records
- public enum values used by requests
- integration events
- public constants for event names, if needed
- public permission code strings used by module metadata
- module metadata descriptors for tooling and docs

Public contract enums use `Unknown = 0` when they exist. Application handlers must validate incoming enum values instead of treating `Unknown` or an undefined numeric value as a valid domain decision.

Avoid:

- `AdminPermission` typed constants
- domain entities
- repository interfaces
- EF Core models
- command handlers
- endpoint handlers

## Module Metadata

Module metadata is a data contract, not runtime discovery.

Use `ModuleDescriptor` in `.Contracts` when a module has permissions, integration events, inbound subscriptions, cache entries, task metadata, or a persistence schema that should be visible to tests, docs, or tooling.

`Name` is the module identity used for composition, observability, and cross-module metadata. `AdminSurfaceName` is optional and exists for modules whose public administration surface intentionally differs from the module identity, such as the `Administration` module exposing `admin.*` commands and permissions while the module remains named `administration`.

Author descriptors through the builder:

```csharp
public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
    .Create(Name)
    .WithSchema(Schema)
    .WithPermission(new ModulePermissionDescriptor("catalog.items.read", "Read catalog items.", scopeRequirement: PermissionScopeRequirement.Scoped))
    .WithPublishedEvent<CatalogItemCreatedIntegrationEvent>()
    .WithCacheEntries([
        new ModuleCacheDescriptor(ItemsCacheEntry, CacheScope.Scope, [ItemsCacheTag]),
        new ModuleCacheDescriptor(ItemCacheEntry, CacheScope.Scope, [ItemsCacheTag]),
    ])
    .Build();
```

Permission scope requirements describe where a permission may be granted. Use `Global` for global-only operations, `Scoped` when a concrete scope is always required, and `GlobalOrScoped` when the same stable permission follows a selected module profile or another explicit host composition choice. The latter does not widen authorization: the operation still requests one exact runtime scope, and grant inheritance remains controlled separately by `PermissionScopeGrantPolicy`.

Prefer the single-item helpers (`WithPermission`, `WithPublishedEvent`, `WithSubscription`, `WithCacheEntry`, `WithTask`) when metadata naturally belongs near one resource or feature. Use the bulk helpers (`WithPermissions`, `WithPublishedEvents`, `WithSubscriptions`, `WithCacheEntries`, `WithTasks`) when a compact list is clearer. Repeated calls merge within the owning capability feature; duplicate metadata still fails through the capability descriptor.

For metadata that belongs to one local type, prefer the attribute-backed helpers:

- put `IntegrationEventNameAttribute` and `IntegrationEventVersionAttribute` on integration event contract types and use `WithPublishedEvent<TEvent>()`;
- put `IntegrationEventHandlerAttribute` on consumer handler types and register them with `AddIntegrationEventHandler<TEvent,THandler>(consumerModule, producerModule)`;
- put split task attributes such as `TaskNameAttribute`, `TaskPayloadVersionAttribute`, `TaskDescriptionAttribute`, `TaskKindAttribute`, and optional `TaskWorkerGroupAttribute`/`SupportsTaskControlAttribute` on serialized task payload contract types and use `WithTask<TPayload>()` plus `AddTaskHandler<TPayload,THandler>(moduleName)`;
- put `[ScopeAware]` from `Gma.Framework.Scoping` on event or task payload contracts that need scope context.

These helpers read attributes from known generic types only. They do not scan assemblies, discover modules, register endpoints, start consumers, or compose workers. Keep permissions and cache metadata descriptor-authored until a single local owner type exists for that metadata.

The root descriptor owns only identity and polymorphic capability features. Capability-specific metadata and extensions live beside the capability:

- `Gma.Framework.Modules` owns the root descriptor, builder, and custom feature base.
- `Gma.Framework.ModuleComposition` owns module profile metadata plus `WithProfile(...)`, `WithProfiles(...)`, `GetCompositionProfiles()`, `SelectModuleProfile(...)`, `ProvideFeature(...)`, `RequireFeature(...)`, `RequireModule(...)`, and `ValidateModuleComposition()`.
- `Gma.Framework.Permissions` owns permission metadata plus `WithPermission(...)`, `WithPermissions(...)`, and `GetPermissions()`.
- `Gma.Framework.Naming` owns low-level kebab-case segment, module-name, and tenant-id normalization shared by domain events, API/admin composition, CLI command ownership, modules, messaging, caching, and task metadata.
- `Gma.Framework.Messaging` owns published-event and subscription metadata plus `IntegrationEventNameAttribute`, `IntegrationEventVersionAttribute`, `IntegrationEventHandlerAttribute`, `WithPublishedEvent(...)`, `WithPublishedEvent<TEvent>()`, `WithPublishedEvents(...)`, `WithSubscription(...)`, `WithSubscription<TEvent>(producerModule, handlerName)`, `WithSubscriptions(...)`, `GetPublishedEvents()`, and `GetSubscriptions()`.
- `Gma.Framework.Caching` owns cache metadata plus `WithCacheEntry(...)`, `WithCacheEntries(...)`, and `GetCacheEntries()`.
- `Gma.Framework.Caching.Cqrs` owns the optional command pipeline behavior that flushes deferred invalidations after successful CQRS unit-of-work commits.
- `Gma.Framework.Tasks` owns task metadata plus `TaskNameAttribute`, `TaskPayloadVersionAttribute`, `TaskDescriptionAttribute`, `TaskKindAttribute`, `TaskWorkerGroupAttribute`, `SupportsTaskControlAttribute`, `WithTask(...)`, `WithTask<TPayload>()`, `WithTasks(...)`, and `GetTasks()`.
- `Gma.Framework.Scoping` owns `[ScopeAware]` and scope metadata readers. `Gma.Framework.Tenancy` owns `[TenantScoped]` as a tenant-facing compatibility contributor. Base messaging/task packages do not reference tenancy.

This is an intentional extension seam. The root `ModuleDescriptor` is sealed so its identity surface stays stable; new optional shared capabilities should add a `ModuleDescriptorFeature` subtype and builder/read extensions in their own namespace rather than adding another root property or subclassing the root descriptor.
Feature keys are stable and capability-prefixed, for example `authorization.permissions`, `messaging.published-events`, `caching.entries`, and `tasks.handlers`. Custom feature keys should follow the same `<capability>.<entry>` shape to avoid collisions across optional packages.

Rules:

- host composition still calls `AddModule<TModule>()`, `AddAdminModule<TModule>()`, or `AddAdminApiModule<TModule>()` explicitly;
- metadata does not cause services, endpoints, consumers, or admin commands to auto-register;
- cross-module metadata uses strings for subjects and handler names unless the consuming module already has an allowed `.Contracts` reference, and those strings must still pass shared integration-event naming validation;
- descriptors should be kept in sync with module docs and architecture tests.

Descriptor value objects validate public metadata at construction/build time and expose constructor-only properties. Invalid module names, permission codes, event subjects, subscription handler names, cache scopes, task names, duplicate entries, or mismatched published-event subjects should fail as soon as the module contract assembly is loaded.

## Composition Profiles

Profiles describe which shape of a reusable module is being composed. They are metadata plus host validation, not runtime discovery.

Example:

```csharp
builder.AddModule<TenancyModule>();
builder.AddAuthModule(AuthProfile.ScopeAware());
builder.ValidateModuleComposition();
```

`AuthProfile.ScopeAware()` selects the `auth` scope-aware profile, provides Auth features, and requires the generic `scoping.context` feature. `Gma.Framework.Tenancy.Scoping` bridges tenant context into scope context, while `TenancyModule` selects its default profile and additionally provides `tenancy.header-resolution` for HTTP header-based tenant resolution.

For tenant-free projects, compose the global profile explicitly:

```csharp
builder.AddAuthModule(AuthProfile.Global("global"));
builder.ValidateModuleComposition();
```

The global profile omits `TenancyModule`; it still needs baseline scoping infrastructure. Compose `Gma.Framework.Infrastructure` (or `Gma.Framework.Scoping.Infrastructure`) so `IScopeContext` resolves to the configured `Scoping:LocalDefaultScopeId`, which Auth sets to the global scope value.

Rules:

- profiles live in the owning module's `.Contracts/Metadata` folder when they are part of the public composition surface;
- front-door projects may offer convenience overloads such as `AddAuthModule(AuthProfile profile)`, but hosts still call them explicitly;
- shared adapters may call `ProvideFeature(...)` for generic capabilities they make available;
- profile validation reports selected modules, provided features, required features, and required modules deterministically;
- profile metadata must not register services, map endpoints, start workers, or scan assemblies.

Current reusable/example profiles:

- `CatalogProfiles.Default` provides `catalog.items` and requires scope context, cache-aside/invalidation services, and outbox infrastructure because its handlers directly depend on those contracts.
- `OrderingProfiles.Default` provides orders plus Ordering-owned catalog item projections. It requires Catalog item facts and scope context, while NATS consumers and task workers remain optional projection-maintenance enhancements.
- `NotificationsProfiles.Default` provides durable notification history and broadcasts and requires scope context. Shared live delivery remains separate through `Gma.Framework.Notifications.Infrastructure`, `Gma.Framework.Notifications.Api`, and `Gma.Framework.Notifications.SignalR`.
- `TaskRuntimeProfiles.Default` describes the admin front door and requires the persisted run store, reporter, and control channel provided by `Gma.Modules.TaskRuntime.Persistence`. Worker-only hosts may compose `Gma.Modules.TaskRuntime.Persistence` with `Gma.Framework.Tasks.Infrastructure` directly and still validate the `tasks.run-store` requirement.

Current adapter feature catalogs live in the capability packages that own the small public contract: `Gma.Framework.Caching.CachingCompositionFeatures`, `Gma.Framework.Messaging.MessagingCompositionFeatures`, `Gma.Framework.Notifications.NotificationsCompositionFeatures`, and `Gma.Framework.Tasks.TasksCompositionFeatures`.

Compiled module projects are also listed in `tests/Architecture.Tests/Support/ArchitectureCatalog.cs`. That catalog feeds architecture tests only and must not be used for runtime composition.

## Domain

`<Module>.Domain` contains business rules.

Allowed examples:

- aggregate roots
- entities
- value objects
- domain events
- domain service interfaces
- domain errors

Avoid:

- EF Core
- ASP.NET Core
- NATS
- logging
- configuration

## Application

`<Module>.Application` contains use cases.

Allowed examples:

- commands and queries
- command/query handlers
- validators
- domain event handlers
- application options
- ports required by handlers

Avoid:

- Minimal API route mapping
- EF Core configurations
- provider-specific infrastructure
- NATS implementation types

## Persistence

`<Module>.Persistence` contains EF Core and database-owned behavior.

Allowed examples:

- DbContext
- entity configurations
- repositories
- module unit of work
- outbox store/writer
- provider selection wiring

Persistence projects must not introduce cross-module foreign keys.

## Api

`<Module>.Api` contains module composition and endpoint mapping.

Allowed examples:

- `IModule` implementation
- endpoint groups
- request-to-command mapping
- result-to-HTTP mapping

Endpoints should be thin. Put behavior in commands, handlers, aggregates, and services.

## Admin

`<Module>.AdminCli` contains optional command-line administration front doors.

Allowed examples:

- `IAdminCliModule` implementation
- `System.CommandLine` command mapping
- usage of typed permissions from `<Module>.Admin.Contracts`
- CLI input/output mapping
- request-to-command/query mapping

Avoid:

- business rules
- EF Core configurations
- direct repository access
- references to other module internals

Admin CLI projects are registered explicitly by `Host.AdminCli`, not by `Host.Api`.

## Admin Contracts

`<Module>.Admin.Contracts` contains optional administration contract helpers shared by `.AdminCli` and `.AdminApi`.

Admin contract projects use:

- `Permissions/` for typed `AdminPermission` wrappers.
- `Operations/` for admin operation name constants.

Allowed examples:

- typed `AdminPermission` constants created from public permission code strings
- admin-only operation metadata used by CLI and admin HTTP

Avoid:

- DTOs used by public API clients
- command/query handlers
- EF Core configurations
- command-line or HTTP route mapping

Public `.Contracts` projects must not reference `Gma.Framework.Administration`. Keep the generic permission code strings there when metadata needs them, and put `AdminPermission` typed constants in `.Admin.Contracts`.

## AdminApi

`<Module>.AdminApi` contains optional administration HTTP routes.

Allowed examples:

- `IAdminApiModule` implementation
- Minimal API route mapping
- admin HTTP request/response records
- request-to-command/query mapping

Avoid:

- business rules
- EF Core configurations
- direct repository access
- references to other module internals

Admin API projects are registered explicitly by `Host.AdminApi`, not by `Host.Api`.

## Adding a Module

Use:

```powershell
.\eng\new-module.ps1 -Name Billing
```

For persistence:

```powershell
.\eng\new-module.ps1 -Name Billing -Persistence -SqlServerMigrations -PostgreSqlMigrations
```

For a richer optional shell:

```powershell
.\eng\new-module.ps1 -Name Billing -Persistence -SqlServerMigrations -PostgreSqlMigrations -Outbox -Inbox -AdminCli -AdminApi -Cache
```

Then decide explicitly whether to register it in `Host.Api`.
When `-RegisterInHost` is used, the script inserts the public API module at the explicit `// module-scaffold:public-api-modules` composition marker in `src/Hosts/Host.Api/Program.cs`.
If the module is committed as compiled code, add its projects to `ArchitectureCatalog` so boundary tests cover it.

The root command is a skeleton wrapper. The framework-owned implementation lives at `eng/new-module.ps1` in the framework repository and is mounted at `gma/framework/eng/new-module.ps1` in the skeleton checkout. The wrapper supplies both the composition repository root and the composition solution filename; framework-owned tooling must not discover or assume a skeleton solution name on its own.

The scaffold follows current runtime conventions:

- application registration extends `IServiceCollection`; runtime/front-door projects pass host configuration only when application options need it;
- application and persistence registration extensions reject null receivers explicitly;
- application DI uses constrained assembly registration for CQRS handlers, validators, and domain-event handlers; integration-event subscriptions stay explicit because subject names and stable handler names are public contracts;
- persistence DI uses repeat-safe registration so public API, admin API, and CLI surfaces can compose the same module safely;
- persistence registration may extend `IHostApplicationBuilder` because it owns provider/configuration wiring;
- persistence registration calls `AddPersistenceOptions(builder.Configuration)` before provider-specific DbContext setup;
- persisted commands should use `ITransactionalCommand<TResponse>`;
- persistence modules register a module-owned `IUnitOfWork` with the lowercase module name;
- persistence scaffolds use shared design-time EF helpers;
- optional inbox/outbox flags scaffold module-owned tables and stores;
- optional admin flags create explicit admin contracts plus CLI/API composition shells;
- outbox projectors should resolve writers through `IOutboxWriterRegistry`.
