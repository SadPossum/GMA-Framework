# Naming Conventions

Use concise names that match project names. Do not root-prefix namespaces with `GMA`.

## Namespaces

Namespace starts with the owning project name:

```text
Gma.Modules.Auth.Application
Gma.Modules.Auth.Domain.Aggregates
Gma.Framework.Messaging.Infrastructure
Gma.Modules.Tenancy.Api
```

Do not use:

```text
GMA.Gma.Modules.Auth.Application
GMA.Modules.Gma.Modules.Auth.Application
```

Architecture tests enforce this convention for source files.

## Projects

Every `.csproj` under `src/` and `tests/` should live in a folder with the same name as the project file:

```text
GMA-Module-Auth/src/Gma.Modules.Auth.Application/Gma.Modules.Auth.Application.csproj
GMA-Module-Auth/tests/Gma.Modules.Auth.Tests/Gma.Modules.Auth.Tests.csproj
```

This keeps project references, namespaces, solution folders, and file-system navigation aligned.

Do not set `<RootNamespace>` or `<AssemblyName>` in project files unless a separate architecture decision explains the exception. The default SDK behavior keeps assembly names, root namespaces, project files, and folders aligned.

Module project names:

```text
Gma.Modules.<Module>.Contracts
Gma.Modules.<Module>.Domain
Gma.Modules.<Module>.Application
Gma.Modules.<Module>.Infrastructure
Gma.Modules.<Module>.Persistence
Gma.Modules.<Module>.Persistence.SqlServerMigrations
Gma.Modules.<Module>.Persistence.PostgreSqlMigrations
Gma.Modules.<Module>.Api
Gma.Modules.<Module>.Admin.Contracts
Gma.Modules.<Module>.AdminCli
Gma.Modules.<Module>.AdminApi
Gma.Modules.<Module>.Tests
```

Reusable module tests live under the module root so a future source repository can carry its own test suite:

```text
GMA-Module-Auth/tests/Gma.Modules.Auth.Tests/Gma.Modules.Auth.Tests.csproj
```

Framework project names:

```text
Gma.Framework.Api
Gma.Framework.AccessControl
Gma.Framework.Administration
Gma.Framework.Administration.AccessControl
Gma.Framework.Administration.Api
Gma.Framework.Administration.Cli
Gma.Framework.Application.Composition
Gma.Framework.Application.Events
Gma.Framework.Application.Events.Infrastructure
Gma.Framework.Api.OpenApi
Gma.Framework.Api.Serilog
Gma.Framework.Permissions
Gma.Framework.AccessControl.AspNetCore
Gma.Framework.Caching
Gma.Framework.Caching.Cqrs
Gma.Framework.Caching.Infrastructure
Gma.Framework.Caching.Redis
Gma.Framework.Cqrs
Gma.Framework.Cqrs.Infrastructure
Gma.Framework.Domain
Gma.Framework.FileManagement
Gma.Framework.FileManagement.LocalStorage
Gma.Framework.FileManagement.Minio
Gma.Framework.Results
Gma.Framework.Infrastructure
Gma.Framework.Logging.Serilog
Gma.Framework.Messaging
Gma.Framework.Messaging.Infrastructure
Gma.Framework.Messaging.Nats
Gma.Framework.Messaging.Nats.Aspire
Gma.Framework.ModuleComposition
Gma.Framework.Modules
Gma.Framework.Naming
Gma.Framework.Numerics
Gma.Framework.Notifications
Gma.Framework.Notifications.Cqrs
Gma.Framework.Notifications.Infrastructure
Gma.Framework.Notifications.Api
Gma.Framework.Notifications.SignalR
Gma.Framework.Observability
Gma.Framework.Observability.Infrastructure
Gma.Framework.Pagination
Gma.Framework.Scoping
Gma.Framework.Scoping.Infrastructure
Gma.Framework.Persistence.EntityFrameworkCore
Gma.Framework.ProjectionRebuild
Gma.Framework.ProjectionRebuild.EntityFrameworkCore
Gma.Framework.ProjectionRebuild.Tasks
Gma.Framework.Realtime
Gma.Framework.Realtime.Infrastructure
Gma.Framework.Realtime.Notifications
Gma.Framework.Runtime
Gma.Framework.Runtime.Infrastructure
Gma.Framework.Security
Gma.Framework.Tasks
Gma.Framework.Tasks.Cqrs
Gma.Framework.Tasks.Infrastructure
Gma.Framework.Tenancy
Gma.Framework.Tenancy.AccessControl.AspNetCore
Gma.Framework.Tenancy.Api.Serilog
Gma.Framework.Tenancy.Caching
Gma.Framework.Tenancy.Cqrs
Gma.Framework.Tenancy.Infrastructure
Gma.Framework.Tenancy.Messaging
Gma.Framework.Tenancy.Messaging.Infrastructure
Gma.Framework.Tenancy.Scoping
Gma.Framework.Tenancy.Tasks
```

## Folders

Recommended module folders:

```text
Commands/
Handlers/
Validation/
Aggregates/
Entities/
Events/
ValueObjects/
Repositories/
Services/
Configurations/
Migrations/
```

Do not create folders before they help navigation.

## Commands and Queries

Commands:

```text
RegisterMemberCommand
SignOutAllCommand
```

Handlers:

```text
RegisterMemberCommandHandler
SignOutAllCommandHandler
```

Keep one handler class per file under `Handlers/`. The file name should match the handler class name, including domain-event projectors and integration-event handlers.

Validators:

```text
RegisterMemberCommandValidator
```

Queries:

```text
GetCurrentMemberQuery
```

Query handlers:

```text
GetCurrentMemberQueryHandler
```

Commands that mutate persistent module state use:

```text
ITransactionalCommand<TResponse>
```

Plain `ICommand<TResponse>` is reserved for command-like operations that do not need a module EF commit.

Module-owned persistence services expose lowercase kebab-case module names:

```text
auth
catalog
ordering
administration
customer-support
```

## Events

Domain events:

```text
MemberRegisteredDomainEvent
```

Integration events:

```text
MemberRegisteredIntegrationEvent
```

Keep one public contract type per file in `.Contracts`, including DTOs, enums, permission code containers, and integration events.

Module metadata lives in `<Module>.Contracts` when it is public to tests, docs, or other modules. Use `Name` for the lowercase module identity and only set `AdminSurfaceName` when the public admin command/permission prefix intentionally differs from the module name.
Descriptor constructor parameters use normal C# camelCase even when they initialize PascalCase properties; named arguments in module metadata should follow the constructor parameter names.

## Enums

Public contract enums, public domain-state enums, and provider/configuration enums that select infrastructure backends use `Unknown = 0`:

```text
Unknown = 0
Active = 1
Disabled = 2
```

Keep existing numeric values stable once the enum is persisted, published in an API/event contract, or bound from configuration. If a persisted enum must be renumbered, add provider-specific compatibility migrations that remap existing rows in the same change. Handlers and option validators should reject unsupported input values explicitly; mapping code must not collapse unknown values into meaningful business states such as `Active` or real providers such as `SqlServer`, `Memory`, or `Redis`.

Public module contract enums own stable lowercase or kebab-case wire names through a module-local `*Names` helper and `[JsonConverter]`. Keep converter code in the owning `.Contracts` package so API, CLI JSON output, integration events, and tests agree without host-wide JSON settings.

Subjects:

```text
{application-namespace}.{module}.{event}.v{version}
```

Example:

```text
gma.auth.member-registered.v1
```

Use lowercase kebab-case for the `{application-namespace}`, `{module}`, `{event}`, and integration-event consumer handler-name segments. The default application namespace is `gma`; set `ApplicationIdentity:Namespace` to the product or bounded system name before creating production NATS streams, cache keys, or dashboards. Module contracts should expose stable logical event names and subject factory methods rather than embedding physical provider subjects by hand. Subscription metadata is validated at composition time, so invalid subject shapes such as extra dots, spaces, or zero-padded versions should fail before a host starts consumers.

## Notifications

Notification names use lowercase dotted segments:

```text
catalog.item-updated
task-runtime.run-completed
```

Keep the name stable and increment the notification payload version when a client-incompatible payload change is introduced. Physical delivery paths and SignalR client method names are host adapter configuration, not module contract names.

## Endpoints

Use lowercase kebab-case paths:

```text
/api/auth/sign-out-all
/api/tenants/current
```

Use PascalCase tags:

```text
Auth
Tenancy
```

## Admin Commands

Use lowercase command names and kebab-case options:

```text
auth members reset-password --member-id <id>
admin roles grant --permission auth.members.read
```

Admin operation names and admin permission codes use dotted lowercase names:

```text
<module>.<resource>.<action>
```

Examples:

```text
auth.members.create
auth.members.read
auth.members.reset-password
admin.roles.manage
```

Admin role names use lowercase slug names. Use letters, numbers, and hyphens only, starting with a letter:

```text
owner
support
tenant-operator
```

## Tests

Test classes that contain `[Fact]`, `[Theory]`, or `[DockerFact]` must end with `Tests`.

Examples:

```text
MemberAggregateTests
AuthLifecycleIntegrationTests
ModuleBoundaryTests
```

Test method names should describe behavior:

```text
Members_and_sessions_are_isolated_by_tenant
Register_login_refresh_and_sign_out_runs_against_sql_server_and_postgre_sql
```

## Scripts

Skeleton composition scripts live in root `eng/` and use kebab-case:

```text
test-fast.ps1
test-docker.ps1
add-migration.ps1
check-source-packages.ps1
new-module.ps1
```

Framework-owned scripts live under the framework repository's root `eng/`. In the skeleton checkout the module scaffolder is mounted at `gma/framework/eng/new-module.ps1`. The skeleton root `eng/new-module.ps1` script is a convenience wrapper around that framework-owned implementation.
