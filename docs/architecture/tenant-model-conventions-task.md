# Tenant Model Conventions And EF Tenant Isolation Task

This is an implementation task brief for a future refactor. It should be handed to an agent together with the current repository checkout.

## Summary

Reduce repeated tenant boilerplate without making tenant ownership invisible.

Status: implemented as a first slice for Auth, Catalog, and Ordering. The implemented EF API uses `TenantAwareDbContext<TContext>` plus `ApplyTenantConventions(modelBuilder)` rather than the originally sketched `tenantFilteringEnabled`/`tenantId` parameter pair. That keeps EF tenant filter values tied to the current DbContext instance and avoids accidentally freezing the first built model's tenant values.

The preferred direction is explicit domain tenancy plus centralized persistence conventions:

- tenant-owned domain models still declare tenant ownership through a base type or marker;
- EF Core tenant column configuration and named query filters are applied by shared helpers;
- write-side tenant guards prevent accidentally saving rows outside the active tenant;
- global or tenant-exempt entities must be explicitly marked and tested.

Do not add runtime shadow `TenantId` properties to arbitrary models just because tenancy is enabled. Tenant id is authoritative domain data in this skeleton and is used by aggregates, events, outbox/inbox, cache keys, task runtime, projections, auth, and admin permissions.

## Current Context

The repo currently uses shared-database tenancy:

- tenant context comes from `ITenantContext` / `ITenantContextAccessor`;
- tenant ids are normalized by `TenantIds`;
- tenant-scoped domain models implement `ITenantScoped`;
- module DbContexts manually apply named EF filters like `TenantFilter`;
- each module repeats `tenantFilteringEnabled` and `tenantId` fields;
- each tenant-scoped entity manually configures `TenantId` max length and indexes.

This is safe but repetitive. The refactor should keep the safety and remove the repetition.

## Core Decision

Use this model:

```text
Explicit tenant ownership in domain/application contracts
  + shared EF conventions for repetitive persistence plumbing
  + strict tests to catch missing tenant declarations or filters
```

Do not use this model:

```text
Tenancy enabled
  -> reflection adds shadow TenantId columns to all models
  -> filters are hidden from module authors
```

That approach weakens domain invariants and makes security-sensitive behavior too implicit.

## Goals

- Make tenant-scoped entities easier to define.
- Make EF tenant filters and tenant property configuration consistent across modules.
- Make tenant/global entity decisions explicit and testable.
- Preserve optional tenancy: projects can still omit the Tenancy module and run with the null/default tenant context.
- Preserve module ownership: modules still own schemas, migrations, indexes, and tenant-local uniqueness decisions.
- Keep host composition explicit. No assembly-wide magic from hosts.

## Non-Goals

- Do not switch from shared-database tenancy to schema-per-tenant or database-per-tenant.
- Do not auto-add `TenantId` as a shadow property to arbitrary entities.
- Do not make runtime configuration change migration/schema shape.
- Do not remove tenant ids from tenant-owned integration-event payloads, cache keys, task requests, projection checkpoints, auth tokens, or admin audit. Messaging infrastructure records should expose generic scope language and use tenant ids only through explicit tenancy bridges.
- Do not create cross-module foreign keys or cross-module EF navigation properties.

## Proposed Public API

Add shared domain base types:

```csharp
public abstract class TenantAggregateRoot<TId> : AggregateRoot<TId>, ITenantScoped
    where TId : notnull
{
    protected TenantAggregateRoot() { }
    protected TenantAggregateRoot(TId id, string tenantId)
        : base(id) => TenantId = TenantIds.Normalize(tenantId);

    public string TenantId { get; protected init; } = string.Empty;
}

public abstract class TenantEntity<TId> : Entity<TId>, ITenantScoped
    where TId : notnull
{
    protected TenantEntity() { }
    protected TenantEntity(TId id, string tenantId)
        : base(id) => TenantId = TenantIds.Normalize(tenantId);

    public string TenantId { get; protected init; } = string.Empty;
}
```

If `protected init` fights EF materialization or existing update patterns, choose the smallest EF-friendly alternative, such as `private set` plus a protected setter method. The important contract is that tenant id is normalized at construction and not mutated by business operations.

Add explicit classification attributes or marker interfaces:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class GlobalEntityAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DisableTenantFilterAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
```

Rules:

- `ITenantScoped` means tenant-owned and filtered.
- `[GlobalEntity]` means intentionally global and never filtered.
- `[DisableTenantFilter]` is rare and must provide a reason.
- a persisted entity with none of these should fail architecture tests unless the module has explicitly listed it as an infrastructure exception.

## Proposed EF Helpers

Add helpers in `Gma.Framework.Persistence.EntityFrameworkCore`:

```csharp
modelBuilder.ApplyTenantConventions(
    tenantFilteringEnabled: this.tenantFilteringEnabled,
    tenantId: this.tenantId);
```

The helper should:

- discover model entity types assignable to `ITenantScoped`;
- configure `TenantId` as required with `TenantIds.MaxLength`;
- apply named query filter `TenantFilter`;
- skip `[GlobalEntity]`;
- allow `[DisableTenantFilter]` only when a non-empty reason is provided;
- throw during model creation if an entity cannot be safely classified;
- avoid touching owned entity types unless they are explicitly mapped as normal tables.

Add a lower-level helper too if needed:

```csharp
modelBuilder.Entity<T>().ApplyTenantFilter(tenantFilteringEnabled, tenantId);
```

This gives modules an escape hatch for complex mappings while keeping the common path central.

## Query Filter Requirements

The generated filter must be equivalent to:

```csharp
!tenantFilteringEnabled || entity.TenantId == tenantId
```

Keep the filter named `TenantFilter` so EF Core named filter behavior stays consistent with the current code.

If expression generation uses reflection, keep it bounded and tested:

- only inspect EF model types in the current `ModelBuilder`;
- only target `ITenantScoped`;
- do not scan arbitrary assemblies from the host;
- do not infer module registration from type names.

## Write-Side Guard

Add a shared guard through one of these approaches:

- `SaveChangesInterceptor`; or
- a protected helper called by module DbContexts or unit of work before commit.

The guard should:

- inspect added/modified entities that implement `ITenantScoped`;
- validate `TenantId` with `TenantIds.TryNormalize`;
- when `ITenantContext.IsEnabled` is true, reject rows whose `TenantId` differs from the active tenant;
- skip `[GlobalEntity]`;
- provide an explicit, documented bypass only for migrations/design-time or module-owned repair jobs.

Prefer failing closed with a clear exception over silently changing tenant ids.

## Module Refactor Scope

Refactor representative modules first, then repeat if the pattern is stable:

- `Auth`
- `Catalog`
- `Ordering`

Expected changes:

- replace repeated `TenantId` boilerplate with `TenantAggregateRoot<TId>` or `TenantEntity<TId>` where it improves clarity;
- keep explicit tenant validation in factories where tenant is part of a business invariant;
- replace manual DbContext tenant filters with `ApplyTenantConventions(...)`;
- keep module-specific tenant-local indexes in module configurations;
- regenerate provider-specific migrations only if the EF model changes.

Be conservative with infrastructure records such as outbox, inbox, task runs, audit entries, checkpoints, and control-plane tables. Some of these are tenant-associated but not necessarily tenant-owned domain entities. Classify each explicitly instead of forcing them into the same base type.

## Architecture Tests

Add tests that fail when tenant behavior is accidental.

Required checks:

- persisted module entities are classified as `ITenantScoped`, `[GlobalEntity]`, `[DisableTenantFilter]`, or documented infrastructure exceptions;
- `ITenantScoped` EF entities have a `TenantId` property with max length `TenantIds.MaxLength`;
- `ITenantScoped` EF entities have the named `TenantFilter`;
- no entity uses `[DisableTenantFilter]` with an empty reason;
- domain projects do not reference EF Core or ASP.NET;
- modules still do not reference other module internals;
- default hosts do not implicitly scan modules for tenancy.

If runtime inspection of EF metadata is practical, assert the actual model rather than relying only on source-text checks.

## Unit Tests

Add or update unit tests for:

- `TenantIds` normalization remains the single tenant id rule;
- base tenant aggregate/entity constructors normalize and reject invalid tenant ids;
- EF helper applies filters only to `ITenantScoped` entities;
- `[GlobalEntity]` entities are not filtered;
- `[DisableTenantFilter]` requires a reason;
- write guard rejects missing, invalid, or mismatched tenant ids;
- write guard allows matching tenant ids;
- null/default tenant context keeps tenant-free projects running.

## Integration Tests

Add focused integration tests against at least one real provider, and both providers if migrations change:

- tenant A cannot read tenant B rows after convention-based filters are applied;
- tenant mismatch on write fails before commit;
- global/control-plane entities remain readable when appropriate;
- Auth, Catalog, and Ordering tenant isolation still pass after refactor.

Do not run full Docker suites unless the slice changes provider mappings, migrations, or integration behavior. Prefer focused tests during development and full validation before commit.

## Documentation Updates

Update these docs with the final implemented approach:

- `src/Framework/docs/architecture/persistence-and-tenancy.md`
- `src/Modules/Tenancy/docs/README.md`
- `src/Framework/docs/templates/module.md`
- `src/Framework/docs/guidelines/development-guidelines.md`
- `src/Framework/docs/guidelines/testing-guidelines.md`

Document the allowed magic explicitly:

- reflection is allowed only inside shared EF model helpers;
- modules must still opt into tenant ownership through base types or markers;
- hosts must not infer module registration through tenancy scanning.

## Suggested Implementation Loop

1. Inspect current tenant usage in Auth, Catalog, Ordering, Administration, TaskRuntime, outbox/inbox, cache, tasks, and projection rebuild code.
2. Write temporary notes for entity classification decisions before editing.
3. Add shared base types and classification attributes.
4. Add EF convention helper and tests using a tiny test DbContext.
5. Add write-side guard and tests.
6. Refactor one module, preferably Catalog, because it is small and tenant-scoped.
7. Run focused tests and migration drift check if EF model shape changed.
8. Refactor Auth and Ordering.
9. Add architecture tests for classification and filter presence.
10. Update docs and templates.
11. Run targeted validation first, then full validation only if the slice affects broad persistence behavior.

## Acceptance Criteria

- Tenant-owned domain models remain visibly tenant-owned.
- Module DbContexts no longer repeat manual tenant filter setup for every entity.
- Tenant query filters are convention-backed and tested.
- Tenant write mismatches fail closed before commit.
- Global or exempt entities are explicit and documented.
- Tenant-free host composition still works.
- SQL Server and PostgreSQL migrations stay clean.
- Docs explain the boundary between useful convention and unsafe magic.
