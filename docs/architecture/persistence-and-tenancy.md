# Persistence and Tenancy

Persistence is module-owned. The shared infrastructure provides abstractions and provider selection, but modules own their DbContexts, schemas, migrations, repositories, and outbox tables.

## Providers

Supported providers:

- SQL Server
- PostgreSQL

Default provider:

```json
{
  "Persistence": {
    "Provider": "SqlServer"
  }
}
```

Connection keys:

- `ConnectionStrings:SqlServer`
- `ConnectionStrings:PostgreSql`

Persistence configuration is validated at startup by modules that register persistence. `Persistence:Provider` must be `SqlServer` or `PostgreSql`, and the matching provider connection string must be present. Shared infrastructure alone does not require database configuration.

## Module-Owned Migrations

Each module with persistence should have provider-specific migration projects:

```text
<Module>.Persistence.SqlServerMigrations/
<Module>.Persistence.PostgreSqlMigrations/
```

Auth uses:

```text
auth.__ef_migrations_history
```

as its schema-local migration history table.

## Adding Migrations

```powershell
.\eng\add-migration.ps1 -Module Auth -Provider SqlServer -Name AddExample
.\eng\add-migration.ps1 -Module Auth -Provider PostgreSql -Name AddExample
.\eng\add-migration.ps1 -Module Catalog -Provider SqlServer -Name AddExample
```

`eng/add-migration.ps1` works for reusable GMA modules mounted under `gma/modules` and app-owned modules under `src/Modules` when they expose matching persistence and provider migration projects. It discovers the DbContext from the persistence project; pass `-Context <Name>DbContext` when discovery is ambiguous.

Create migrations for both providers when a schema change is provider-agnostic.

Check model-to-migration drift before finishing persistence work:

```powershell
.\eng\check-migrations.ps1 -NoBuild
```

The script discovers every provider migration project under `src/Modules` and `gma/modules`, then runs EF's pending-model-change check against the matching design-time factory. `eng/verify.ps1` includes this check by default after build; pass `-SkipMigrationCheck` only for a deliberately narrow local loop.

Design-time factories live in provider-specific migration projects, not runtime persistence projects:

- SQL Server factory: `<Module>.Persistence.SqlServerMigrations`
- PostgreSQL factory: `<Module>.Persistence.PostgreSqlMigrations`

The pinned local `dotnet-ef` tool in `.config/dotnet-tools.json` uses the same version as `Microsoft.EntityFrameworkCore.Design`; update both together when EF Core is upgraded.

`eng/add-migration.ps1` uses the selected migration project as both the EF target project and startup project. Factories should use `DesignTimeDbContextOptionsFactory.CreateSqlServerOptions(...)` or `CreatePostgreSqlOptions(...)` from `Gma.Framework.Persistence.EntityFrameworkCore` so default local connection strings and migration history configuration stay consistent.

`Microsoft.EntityFrameworkCore.Design` belongs only in provider migration projects.

## Applying Migrations

Do not auto-apply migrations from `Host.Api` startup.

Use explicit migration steps in tests, local development, or deployment automation. Integration tests use `MigrateAsync`, not `EnsureCreated`.

## Indexing Tenant-Scoped Reads

Tenant-scoped tables should index the tenant id together with the next selective read-path column. Examples:

- unique tenant-local identifiers, such as `(TenantId, Sku)`;
- tenant-local list ordering, such as `(TenantId, RegisteredAtUtc)`;
- tenant-scoped authorization lookups, such as `(PrincipalId, TenantId)` when global and tenant assignments are checked together.

Do not add cross-module foreign keys to optimize reads. Use local projections, duplicated identifiers, or module-owned indexes instead.

## Scope Model Conventions

Scope ownership is explicit in the model and conventional in EF plumbing. In a tenant-aware host, the active tenant becomes the active scope through the Tenancy-to-Scoping bridge.

Scope-owned domain models should inherit one of the shared base types from `Gma.Framework.Domain.Models`:

- `ScopedAggregateRoot<TId>` for scope-owned aggregate roots;
- `ScopedEntity<TId>` for scope-owned child entities or local projections.

Both base types implement `IScopedEntity`, normalize scope ids through `ScopeIds`, and keep scope ids immutable to business operations. A type may also implement `IScopedEntity` directly when it already has a different base type.

Scope-aware EF contexts should inherit `ScopeAwareDbContext<TContext>` and call:

```csharp
this.ApplyScopeConventions(modelBuilder);
```

The shared convention configures `ScopeId` as required with `ScopeIds.MaxLength` and applies the named EF Core filter `ScopeFilter` to every mapped `IScopedEntity` type in that context. `ScopeAwareDbContext<TContext>` reads the active value through `IScopeContext`, so reusable modules can run in tenant-aware and tenant-free hosts while their persisted isolation key stays generic. The only reflection here is bounded to the current EF model; hosts must not scan assemblies to discover modules or scope behavior.

Do not add shadow `TenantId` or `ScopeId` properties to arbitrary models. Scope id is authoritative data used by reusable aggregates, scope-owned domain/integration events, cache keys, task requests, projection checkpoints, auth tokens, and admin audit. Tenancy adapters may expose that same value as a tenant id at tenant-facing boundaries.

Infrastructure records are classified deliberately. Outbox/inbox rows use a generic message `ScopeId` in code so base messaging stays tenant-neutral; the current EF mapping stores it in the existing `TenantId` column for compatibility. These rows are not tenant-filtered by default because publishers and consumers need module-owned cross-scope runtime visibility.

`ScopeAwareDbContext<TContext>` also validates added and modified `IScopedEntity` entities before `SaveChanges`: scope ids must be valid, normalized, and, when scoping is enabled, equal to the active scope context.

## Unit of Work

EF Core `DbContext` is the practical unit of work. A module unit of work wraps:

- domain event collection;
- domain event dispatch;
- outbox writes;
- EF Core commit;
- domain event clearing after successful commit.

## Tenancy Strategy

V1 tenancy uses a shared database:

- scope-owned entities implement `IScopedEntity`, usually via `ScopedAggregateRoot<TId>` or `ScopedEntity<TId>`;
- tenant-scoped endpoints require the configured tenant header, usually `X-Tenant-Id`, and the tenancy bridge exposes that tenant as the active scope;
- scope-aware EF conventions isolate reads and write guards reject mismatches;
- local development can use the `default` tenant.

Tenancy configuration is validated at startup. `Tenancy:HeaderName` must be a valid HTTP header name and `Tenancy:LocalDefaultTenantId` must be non-empty, no longer than 128 characters, and free of whitespace or control characters. Scoping configuration is validated separately; `Scoping:HeaderName` controls `RequireScope()` and `Scoping:LocalDefaultScopeId` is the fallback scope for global/non-tenant hosts.

Tenant contracts live in `Gma.Framework.Tenancy` so API, persistence, task-runtime, and bridge adapters can depend on tenant context without depending on the broader CQRS/application contract package.
Caching stays tenant-neutral by default; `Gma.Framework.Tenancy.Caching` is the explicit runtime bridge that resolves tenant-owned cache scope values from `ITenantContext`.
Messaging stays tenant-neutral by default; `ScopedIntegrationEvent` is the generic contract shape, `Gma.Framework.Tenancy.Messaging` is the explicit contract bridge for tenant-owned integration events, and `Gma.Framework.Tenancy.Messaging.Infrastructure` turns tenant events into message scope metadata and sets tenant context for consumers.

`ITenantContextAccessor` is mutable runtime state, not authoritative domain data. Host/front-door/runtime boundaries set it from the request, CLI operation, or tenant-aware integration event and clear it before applying a new tenant so reused scopes cannot inherit a stale tenant id. Reusable domain entities and integration events store normalized scope ids; tenancy-specific adapters may project that value as a tenant id.

## No Cross-Module Foreign Keys

Modules must not create foreign keys to another module's tables.

Use one of these instead:

- duplicate stable identifiers;
- consume another module's contract;
- publish/consume integration events;
- use an application-level consistency check.

## Tenant Safety Checklist

For tenant-scoped behavior:

- endpoint calls `.RequireScope()` for reusable scope-aware modules or `.RequireTenant()` for tenant-facing Tenancy endpoints;
- command includes scope or tenant context implicitly or explicitly;
- aggregate inherits `ScopedAggregateRoot<TId>` or otherwise implements `IScopedEntity`;
- DbContext inherits `ScopeAwareDbContext<TContext>` and calls `ApplyScopeConventions(modelBuilder)`;
- repository queries rely on the named `ScopeFilter` unless a documented module-owned runtime path intentionally bypasses filters;
- tests prove tenant isolation.
