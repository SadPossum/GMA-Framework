# Architecture Overview

GMA Framework is a set of reusable capability packages for modular monolith applications. It keeps deployment simple while preserving module boundaries that can survive growth.

## Goals

- Keep domains and features optional.
- Make modules easy to add, remove, and replace.
- Avoid assembly scanning and hidden registration.
- Keep domain/application code independent of web, EF Core, and NATS details.
- Use reliable outbox publishing for cross-boundary integration events.
- Support shared-database tenancy from the start.
- Keep metrics, logging, and tracing vendor-neutral inside modules.
- Keep caching explicit, optional, tenant-safe, and provider-independent inside modules.
- Keep user notifications optional and front-door focused; backend integration still goes through events/outbox/inbox.

## Current Shape

```text
GMA-Framework/
  docs/
  eng/
  src/
    Administration/
      Gma.Framework.Administration/
      Gma.Framework.Administration.Api/
      Gma.Framework.Administration.Cli/
    Api/
      Gma.Framework.Api/
      Gma.Framework.Api.OpenApi/
      Gma.Framework.Api.Serilog/
    Application/
      Gma.Framework.Application.Composition/
      Gma.Framework.Application.Events/
      Gma.Framework.Application.Events.Infrastructure/
    Caching/
      Gma.Framework.Caching/
      Gma.Framework.Caching.Cqrs/
      Gma.Framework.Caching.Infrastructure/
      Gma.Framework.Caching.Redis/
    Cqrs/
      Gma.Framework.Cqrs/
      Gma.Framework.Cqrs.Infrastructure/
    Domain/
      Gma.Framework.Domain/
    FileManagement/
      Gma.Framework.FileManagement/
      Gma.Framework.FileManagement.LocalStorage/
      Gma.Framework.FileManagement.Minio/
    Infrastructure/
      Gma.Framework.Infrastructure/
    Logging/
      Gma.Framework.Logging.Serilog/
    Messaging/
      Gma.Framework.Messaging/
      Gma.Framework.Messaging.Infrastructure/
      Gma.Framework.Messaging.Nats/
      Gma.Framework.Messaging.Nats.Aspire/
    Modules/
      Gma.Framework.ModuleComposition/
      Gma.Framework.Modules/
    Naming/
      Gma.Framework.Naming/
    Notifications/
      Gma.Framework.Notifications/
      Gma.Framework.Notifications.Api/
      Gma.Framework.Notifications.Cqrs/
      Gma.Framework.Notifications.Infrastructure/
      Gma.Framework.Notifications.SignalR/
    Numerics/
      Gma.Framework.Numerics/
    Observability/
      Gma.Framework.Observability/
      Gma.Framework.Observability.Infrastructure/
    Pagination/
      Gma.Framework.Pagination/
    Persistence/
      Gma.Framework.Persistence.EntityFrameworkCore/
    ProjectionRebuild/
      Gma.Framework.ProjectionRebuild/
      Gma.Framework.ProjectionRebuild.EntityFrameworkCore/
      Gma.Framework.ProjectionRebuild.Tasks/
    Results/
      Gma.Framework.Results/
    Runtime/
      Gma.Framework.Runtime/
      Gma.Framework.Runtime.Infrastructure/
    Security/
      Gma.Framework.AccessControl/
      Gma.Framework.Authorization/
      Gma.Framework.Security/
    Tasks/
      Gma.Framework.Tasks/
      Gma.Framework.Tasks.Cqrs/
      Gma.Framework.Tasks.Infrastructure/
    Tenancy/
      Gma.Framework.Tenancy/
      Gma.Framework.Tenancy.Api.Serilog/
      Gma.Framework.Tenancy.Caching/
      Gma.Framework.Tenancy.Cqrs/
      Gma.Framework.Tenancy.Infrastructure/
      Gma.Framework.Tenancy.Messaging/
      Gma.Framework.Tenancy.Messaging.Infrastructure/
      Gma.Framework.Tenancy.Tasks/
  tests/
    Gma.Framework.Tests/
```

In the `GMA-Skeleton` composition repository, this framework repository is mounted at `gma/framework`. Reusable modules are mounted beside it under `gma/modules/<alias>`, while skeleton-owned example modules remain under `src/Modules/<Example>`.

Current logical module roots in the skeleton are `Administration/`, `Auth/`, `Files/`, `Notifications/`, `TaskRuntime/`, `Tenancy/`, `Catalog/`, `Ordering/`, and `TaskSamples/`. Reusable roots resolve through source-root properties to mounted repositories; example roots resolve to skeleton-owned source folders.

```text
GMA-Skeleton/
  gma/
    framework/
    modules/
      administration/
      auth/
      files/
      notifications/
      task-runtime/
      tenancy/
  src/
    Host.Api/
    Host.AdminCli/
    Host.AdminApi/
    Host.Worker/
    AppHost/
    ServiceDefaults/
    Modules/
      Catalog/
      Ordering/
      TaskSamples/
  tests/
    Architecture.Tests/
    Integration.Tests/
    ServiceDefaults.Tests/
```

## Runtime Composition In The Skeleton

`Host.Api` is the composition root:

1. Adds optional infrastructure adapters such as Redis, OpenAPI, caching runtime, messaging runtime, and configured NATS publishing.
2. Adds shared core infrastructure.
3. Optionally starts NATS/JetStream publishing through the messaging adapter.
4. Explicitly registers optional modules.
5. Maps module endpoints.

Current host registration:

```csharp
builder.AddUserNotificationsCqrs(); // post-commit flush for queued notification requests
builder.AddRedisCaching(); // no-op unless Redis caching is enabled
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging(); // tenant-aware event scope/context bridge
builder.AddConfiguredNatsJetStreamMessaging(); // no-op unless NATS publishing is enabled
builder.AddUserNotificationServerSentEvents();
builder.AddUserNotificationSignalR();
builder.Services.AddApiSecurityDefaults(); // no default scheme; Auth or another adapter supplies one
builder.AddModule<TenancyModule>();
builder.AddAuthModule(AuthProfile.TenantScoped());
builder.AddGmaOpenApi();
builder.ValidateModuleComposition();
app.UseGmaOpenApi(); // serves Swagger only in Development
app.MapModules();
app.MapUserNotificationServerSentEvents();
app.MapUserNotificationSignalR();
```

`Host.AdminCli` is a separate optional composition root for administrative commands:

```csharp
builder.AddGmaAdministrationCli();
builder.AddRedisCaching(); // no-op unless Redis caching is enabled
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddMessagingInfrastructure(); // outbox writer registry without hosted publishers
builder.AddTenantAwareMessaging(); // tenant-aware event scope/context bridge
builder.AddAdminModule<AdministrationAdminCliModule>();
builder.AddAuthAdminModule(AuthProfile.TenantScoped());
builder.ValidateModuleComposition();
```

It does not map public API endpoints.

`Host.AdminApi` is a separate optional composition root for administrative HTTP APIs:

```csharp
builder.Services.AddGmaAdministrationApi(builder.Configuration);
builder.AddRedisCaching(); // no-op unless Redis caching is enabled
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging(); // tenant-aware event scope/context bridge
builder.AddAdminApiModule<AdministrationAdminApiModule>();
builder.AddAuthAdminApiModule(AuthProfile.TenantScoped());
builder.AddGmaOpenApi();
builder.ValidateModuleComposition();
app.MapAdminApiModules();
```

`Host.Api` still does not map admin routes.

`Host.Worker` is an optional generic-host composition root for background infrastructure:

```csharp
builder.AddRedisCaching(); // no-op unless Redis caching is enabled
builder.AddCachingCqrs();
builder.AddGmaInfrastructure();
builder.AddTenantCaching();
builder.AddMessagingInfrastructure();
builder.AddTenantAwareMessaging();
builder.AddConfiguredNatsJetStreamMessaging(); // no-op unless NATS publishing is enabled
builder.AddConfiguredNatsJetStreamConsumers(); // called only when NatsConsumers:Enabled=true
builder.AddTaskWorkerRuntime(); // called only when Tasks:Worker:Enabled=true
builder.ValidateModuleComposition();
```

Worker module groups are explicit configuration switches under `Worker:Modules`. The checked-in defaults compose no module groups and start no background loops. Deployments opt into only the modules this process should drain or execute, for example `Worker:Modules:Auth=true` for Auth outbox publishing, `Worker:Modules:Ordering=true` for Ordering consumers/projection rebuilds, and `Worker:Modules:TaskRuntime=true` when task workers need the persisted run store. `Host.Worker` does not map public or admin business endpoints.

Runtime project dependency ownership:

- `Host.Api` and `Host.AdminApi` should have no direct package references; they compose module front doors and shared adapters.
- `Host.AdminCli` owns only CLI-hosting packages and composes admin CLI modules plus shared runtime adapters.
- `Host.Worker` owns only generic-host composition plus explicit background module/application/persistence references; it does not reference API/AdminApi front doors.
- `ServiceDefaults` owns local observability, service-discovery, HTTP resilience, and Prometheus scrape endpoint packages.
- `AppHost` owns Aspire hosting packages and references runnable hosts, not module internals.

## Dependency Direction

Allowed direction:

```text
Host.Api
  -> Modules.*.Api
  -> Modules.*.Application
  -> Modules.*.Domain

Modules.*.Persistence
  -> Modules.*.Application
  -> Modules.*.Domain

Gma.Framework.AccessControl
  -> Gma.Framework.Naming

Gma.Framework.Administration
  -> Gma.Framework.Naming
  -> Gma.Framework.Results
  -> Gma.Framework.Runtime
  -> Gma.Framework.Tenancy

Gma.Framework.Administration.Api
  -> Gma.Framework.Administration
  -> Gma.Framework.Api
  -> Gma.Framework.Cqrs
  -> Gma.Framework.Results
  -> Gma.Framework.Naming
  -> Gma.Framework.Security
  -> Gma.Framework.Tenancy

Gma.Framework.Administration.Cli
  -> Gma.Framework.Administration
  -> Gma.Framework.Cqrs
  -> Gma.Framework.Results
  -> Gma.Framework.Naming
  -> Gma.Framework.Runtime

Gma.Framework.Api
  -> Gma.Framework.Results
  -> Gma.Framework.Naming
  -> Gma.Framework.Tenancy

Gma.Framework.Api.OpenApi
  -> no project references

Gma.Framework.Api.Serilog
  -> Gma.Framework.Api
  -> Gma.Framework.Observability

Gma.Framework.Tenancy.Api.Serilog
  -> Gma.Framework.Api.Serilog
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Observability
  -> Gma.Framework.Tenancy

Gma.Framework.Application.Composition
  -> Gma.Framework.Application.Events
  -> Gma.Framework.Cqrs

Gma.Framework.Application.Events
  -> Gma.Framework.Domain

Gma.Framework.Application.Events.Infrastructure
  -> Gma.Framework.Application.Events
  -> Gma.Framework.Domain

Gma.Framework.Authorization
  -> Gma.Framework.Modules
  -> Gma.Framework.Naming

Gma.Framework.Caching
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Modules
  -> Gma.Framework.Naming

Gma.Framework.Caching.Redis
  -> Gma.Framework.Caching
  -> Gma.Framework.ModuleComposition

Gma.Framework.Caching.Cqrs
  -> Gma.Framework.Caching
  -> Gma.Framework.Caching.Infrastructure
  -> Gma.Framework.Cqrs
  -> Gma.Framework.Cqrs.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Results

Gma.Framework.Caching.Infrastructure
  -> Gma.Framework.Caching
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Observability
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Runtime
  -> Gma.Framework.Runtime.Infrastructure

Gma.Framework.Cqrs
  -> Gma.Framework.Results

Gma.Framework.Cqrs.Infrastructure
  -> Gma.Framework.Cqrs
  -> Gma.Framework.Results
  -> Gma.Framework.Naming
  -> Gma.Framework.Observability
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Runtime.Infrastructure

Gma.Framework.Domain
  -> Gma.Framework.Naming
  -> Gma.Framework.Numerics

Gma.Framework.FileManagement
  -> no project references

Gma.Framework.FileManagement.LocalStorage
  -> Gma.Framework.FileManagement
  -> Gma.Framework.ModuleComposition

Gma.Framework.FileManagement.Minio
  -> Gma.Framework.FileManagement
  -> Gma.Framework.ModuleComposition

Gma.Framework.Infrastructure
  -> Gma.Framework.Application.Events.Infrastructure
  -> Gma.Framework.Cqrs.Infrastructure
  -> Gma.Framework.Runtime.Infrastructure
  -> Gma.Framework.Tenancy.Cqrs
  -> Gma.Framework.Tenancy.Infrastructure

Gma.Framework.Logging.Serilog
  -> no project references

Gma.Framework.Messaging
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Modules
  -> Gma.Framework.Naming
  -> Gma.Framework.Numerics

Gma.Framework.Messaging.Infrastructure
  -> Gma.Framework.Messaging
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Observability
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Runtime
  -> Gma.Framework.Runtime.Infrastructure

Gma.Framework.Messaging.Nats
  -> Gma.Framework.Messaging
  -> Gma.Framework.Messaging.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Runtime

Gma.Framework.Messaging.Nats.Aspire
  -> Gma.Framework.Messaging.Nats

Gma.Framework.ModuleComposition
  -> Gma.Framework.Modules
  -> Gma.Framework.Naming

Gma.Framework.Modules
  -> Gma.Framework.Naming

Gma.Framework.Naming
  -> no project references

Gma.Framework.Numerics
  -> no project references

Gma.Framework.Notifications
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Modules
  -> Gma.Framework.Naming

Gma.Framework.Notifications.Cqrs
  -> Gma.Framework.Cqrs
  -> Gma.Framework.Cqrs.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Notifications
  -> Gma.Framework.Notifications.Infrastructure
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Results

Gma.Framework.Notifications.Api
  -> Gma.Framework.Api
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Notifications
  -> Gma.Framework.Security
  -> Gma.Framework.Tenancy

Gma.Framework.Notifications.Infrastructure
  -> Gma.Framework.Naming
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Notifications
  -> Gma.Framework.Observability
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Runtime
  -> Gma.Framework.Runtime.Infrastructure

Gma.Framework.Notifications.SignalR
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Notifications
  -> Gma.Framework.Runtime
  -> Gma.Framework.Security
  -> Gma.Framework.Tenancy

Gma.Framework.Observability
  -> Gma.Framework.Naming

Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Naming
  -> Gma.Framework.Observability
  -> Gma.Framework.Runtime

Gma.Framework.Pagination
  -> no project references

Gma.Framework.Persistence.EntityFrameworkCore
  -> Gma.Framework.Application.Events
  -> Gma.Framework.Cqrs
  -> Gma.Framework.Domain
  -> Gma.Framework.Naming
  -> Gma.Framework.Tenancy

Gma.Framework.ProjectionRebuild
  -> Gma.Framework.Naming
  -> Gma.Framework.Observability
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Runtime

Gma.Framework.ProjectionRebuild.Tasks
  -> Gma.Framework.ProjectionRebuild
  -> Gma.Framework.Tasks

Gma.Framework.ProjectionRebuild.EntityFrameworkCore
  -> Gma.Framework.Naming
  -> Gma.Framework.ProjectionRebuild

Gma.Framework.Results
  -> no project references

Gma.Framework.Runtime
  -> Gma.Framework.Naming

Gma.Framework.Runtime.Infrastructure
  -> Gma.Framework.Naming
  -> Gma.Framework.Runtime

Gma.Framework.Security
  -> no project references

Gma.Framework.Tasks
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Modules
  -> Gma.Framework.Naming

Gma.Framework.Tasks.Cqrs
  -> Gma.Framework.Cqrs
  -> Gma.Framework.Cqrs.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Results
  -> Gma.Framework.Tasks

Gma.Framework.Tasks.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Observability
  -> Gma.Framework.Observability.Infrastructure
  -> Gma.Framework.Runtime
  -> Gma.Framework.Runtime.Infrastructure
  -> Gma.Framework.Tasks

Gma.Framework.Tenancy
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Modules
  -> Gma.Framework.Results

Gma.Framework.Tenancy.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Tenancy

Gma.Framework.Tenancy.Caching
  -> Gma.Framework.Caching
  -> Gma.Framework.Caching.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Tenancy

Gma.Framework.Tenancy.Cqrs
  -> Gma.Framework.Cqrs.Infrastructure
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Observability
  -> Gma.Framework.Tenancy

Gma.Framework.Tenancy.Messaging
  -> Gma.Framework.Messaging
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Tenancy

Gma.Framework.Tenancy.Messaging.Infrastructure
  -> Gma.Framework.Messaging
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Tenancy
  -> Gma.Framework.Tenancy.Messaging

Gma.Framework.Tenancy.Tasks
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Tasks
  -> Gma.Framework.Tasks.Infrastructure
  -> Gma.Framework.Tenancy

Gma.Modules.Files.Contracts
  -> Gma.Framework.FileManagement
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Modules
  -> Gma.Framework.Tenancy

Gma.Modules.Files.Application
  -> Gma.Modules.Files.Contracts
  -> Gma.Framework.AccessControl
  -> Gma.Framework.Application.Composition
  -> Gma.Framework.Cqrs
  -> Gma.Framework.FileManagement
  -> Gma.Framework.Results
  -> Gma.Framework.Runtime
  -> Gma.Framework.Tenancy

Gma.Modules.Files.Api
  -> Gma.Modules.Files.Application
  -> Gma.Modules.Files.Contracts
  -> Gma.Framework.AccessControl
  -> Gma.Framework.Api
  -> Gma.Framework.Cqrs
  -> Gma.Framework.ModuleComposition
  -> Gma.Framework.Naming
  -> Gma.Framework.Results
  -> Gma.Framework.Security
  -> Gma.Framework.Tenancy
```

Cross-module dependencies must go through:

- `<OtherModule>.Contracts`
- integration events
- shared abstractions

Do not reference another module's Domain, Application, Persistence, Infrastructure, or Api project.

## Request Flow

```text
HTTP endpoint
  -> command or query
  -> IRequestDispatcher
  -> pipeline behaviors
  -> handler
  -> aggregate/repository
  -> unit of work
  -> domain event dispatcher
  -> outbox writer
  -> EF Core SaveChanges
  -> hosted outbox publisher
  -> IEventBus
  -> NATS JetStream
```

Observability follows a separate adapter flow:

```text
ILogger / IMeterFactory / ActivitySource
  -> ServiceDefaults
  -> optional Prometheus scrape and OTLP export
  -> deployment-selected backends such as Loki
```

Caching follows the same contract-first rule:

```text
module query handler -> IApplicationCache -> HybridCache -> optional Redis L2
module command handler -> ICacheInvalidationQueue -> post-commit flush
```

User notifications have two explicit paths. Best-effort live delivery follows a front-door rule with post-commit command requests:

```text
transactional command handler -> IUserNotificationRequestQueue
  -> Gma.Framework.Notifications.Cqrs post-commit flush
  -> IUserNotificationPublisher
  -> in-process notification feed
  -> optional SSE endpoint / optional SignalR hub
```

Durable notification history uses the optional Notifications module:

```text
source module command -> source module outbox
  -> UserNotificationRequestedIntegrationEvent
  -> NATS consumer
  -> Notifications inbox + user_notifications
  -> current-user/admin history stream
```

For example, Catalog publishes item facts, Ordering consumes those facts into its local projection, Ordering decides which order owners are affected, and Ordering publishes addressed notification requests. Notifications stores and streams the addressed messages; it does not define Catalog or Ordering visibility rules.

Durable backend facts still flow through domain events, outbox, NATS, and inbox. SignalR/SSE must not become a module integration path.

Administration follows the same explicit front-door rule:

```text
Host.AdminCli -> *.AdminCli -> *.Application -> *.Domain
Host.AdminApi -> *.AdminApi -> *.Application -> *.Domain
                 |
                 -> *.Admin.Contracts
                 -> Gma.Framework.Administration contracts
```

## Why This Shape

The skeleton favors explicitness over magic. A module is optional only if the host has to opt into it. A boundary is real only if architecture tests and project references make it hard to accidentally cross it.
