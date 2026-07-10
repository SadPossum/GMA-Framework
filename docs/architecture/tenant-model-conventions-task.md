# Scope Model Conventions And EF Isolation Task

This is the implementation brief for the scope model conventions refactor. It is kept as architecture context for why the reusable primitive is scope-based while tenancy remains a product-facing adapter.

## Summary

Reduce repeated isolation boilerplate without making tenant or scope ownership invisible.

Status: implemented as a first slice for Auth, Catalog, Ordering, and Notifications. The implemented EF API uses `ScopeAwareDbContext<TContext>` plus `ApplyScopeConventions(modelBuilder)` rather than the originally sketched `tenantFilteringEnabled`/`tenantId` parameter pair. That keeps EF scope filter values tied to the current DbContext instance and avoids accidentally freezing the first built model's scope values.

The preferred direction is explicit domain tenancy plus centralized persistence conventions:

- scope-owned domain models still declare isolation through a base type or marker;
- EF Core scope column configuration and named query filters are applied by shared helpers;
- write-side scope guards prevent accidentally saving rows outside the active scope;
- global or tenant-exempt entities must be explicitly marked and tested.

Do not add runtime shadow `TenantId` or `ScopeId` properties to arbitrary models just because tenancy or scoping is enabled. Scope id is authoritative domain data in reusable modules and is used by aggregates, events, outbox/inbox, cache keys, task runtime, projections, auth, and admin permissions. Tenant ids appear at tenant-facing boundaries and explicit tenancy bridges.

## Current Context

The repo currently uses shared-database tenancy:

- tenant context comes from `ITenantContext` / `ITenantContextAccessor` and is bridged into `IScopeContext`;
- reusable scope ids are normalized by `ScopeIds`;
- scope-owned domain models implement `IScopedEntity`;
- module DbContexts apply named EF filters like `ScopeFilter`;
- earlier module DbContexts repeated `tenantFilteringEnabled` and `tenantId` fields;
- earlier tenant-scoped entities manually configured `TenantId` max length and indexes.

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
  -> reflection adds shadow TenantId or ScopeId columns to all models
  -> filters are hidden from module authors
```

That approach weakens domain invariants and makes security-sensitive behavior too implicit.

## Goals

- Make scope-owned entities easier to define.
- Make EF scope filters and scope property configuration consistent across modules.
- Make tenant/global entity decisions explicit and testable.
- Preserve optional tenancy: projects can still omit the Tenancy module and run with the null/default tenant context.
- Preserve module ownership: modules still own schemas, migrations, indexes, and tenant-local uniqueness decisions.
- Keep host composition explicit. No assembly-wide magic from hosts.

## Non-Goals

- Do not switch from shared-database tenancy to schema-per-tenant or database-per-tenant.
- Do not auto-add `TenantId` or `ScopeId` as a shadow property to arbitrary entities.
- Do not make runtime configuration change migration/schema shape.
- Do not remove scope ids from reusable integration-event payloads, cache keys, task requests, projection checkpoints, auth tokens, or admin audit. Messaging infrastructure records should expose generic scope language and use tenant ids only through explicit tenancy bridges.
- Do not create cross-module foreign keys or cross-module EF navigation properties.

## Proposed Public API

Add shared domain base types:

```csharp
public abstract class ScopedAggregateRoot<TId> : AggregateRoot<TId>, IScopedEntity
    where TId : notnull
{
    protected ScopedAggregateRoot() { }
    protected ScopedAggregateRoot(TId id, string scopeId)
        : base(id) => ScopeId = ScopeIds.Normalize(scopeId);

    public string ScopeId { get; private set; } = string.Empty;
}

public abstract class ScopedEntity<TId> : Entity<TId>, IScopedEntity
    where TId : notnull
{
    protected ScopedEntity() { }
    protected ScopedEntity(TId id, string scopeId)
        : base(id) => ScopeId = ScopeIds.Normalize(scopeId);

    public string ScopeId { get; private set; } = string.Empty;
}
```

The important contract is that scope id is normalized at construction and not mutated by business operations.

Add explicit classification attributes or marker interfaces:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class GlobalEntityAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DisableScopeFilterAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
```

Rules:

- `IScopedEntity` means scope-owned and filtered.
- `[GlobalEntity]` means intentionally global and never filtered.
- `[DisableScopeFilter]` is rare and must provide a reason.
- a persisted entity with none of these should fail architecture tests unless the module has explicitly listed it as an infrastructure exception.

## Proposed EF Helpers

Add helpers in `Gma.Framework.Persistence.EntityFrameworkCore`:

```csharp
this.ApplyScopeConventions(modelBuilder);
```

The helper should:

- discover model entity types assignable to `IScopedEntity`;
- configure `ScopeId` as required with `ScopeIds.MaxLength`;
- apply named query filter `ScopeFilter`;
- skip `[GlobalEntity]`;
- allow `[DisableScopeFilter]` only when a non-empty reason is provided;
- throw during model creation if an entity cannot be safely classified;
- avoid touching owned entity types unless they are explicitly mapped as normal tables.

Add a lower-level helper too if needed:

```csharp
modelBuilder.Entity<T>().ApplyScopeFilter(this);
```

This gives modules an escape hatch for complex mappings while keeping the common path central.

## Query Filter Requirements

The generated filter must be equivalent to:

```csharp
!context.ScopeFilterEnabled || entity.ScopeId == context.CurrentScopeId
```

Keep the filter named `ScopeFilter` so EF Core named filter behavior stays consistent with the current code.

If expression generation uses reflection, keep it bounded and tested:

- only inspect EF model types in the current `ModelBuilder`;
- only target `IScopedEntity`;
- do not scan arbitrary assemblies from the host;
- do not infer module registration from type names.

## Write-Side Guard

Add a shared guard through one of these approaches:

- `SaveChangesInterceptor`; or
- a protected helper called by module DbContexts or unit of work before commit.

The guard should:

- inspect added/modified entities that implement `IScopedEntity`;
- validate `ScopeId` with `ScopeIds.TryNormalize`;
- when `IScopeContext.IsEnabled` is true, reject rows whose `ScopeId` differs from the active scope;
- skip `[GlobalEntity]`;
- provide an explicit, documented bypass only for migrations/design-time or module-owned repair jobs.

Prefer failing closed with a clear exception over silently changing scope ids.

## Module Refactor Scope

Refactor representative modules first, then repeat if the pattern is stable:

- `Auth`
- `Catalog`
- `Ordering`

Expected changes:

- replace repeated `ScopeId` boilerplate with `ScopedAggregateRoot<TId>` or `ScopedEntity<TId>` where it improves clarity;
- keep explicit tenant validation in factories where tenant is part of a business invariant;
- replace manual DbContext tenant filters with `ApplyScopeConventions(...)`;
- keep module-specific tenant-local indexes in module configurations;
- regenerate provider-specific migrations only if the EF model changes.

Be conservative with infrastructure records such as outbox, inbox, task runs, audit entries, checkpoints, and control-plane tables. Some of these are tenant-associated but not necessarily tenant-owned domain entities. Classify each explicitly instead of forcing them into the same base type.

## Architecture Tests

Add tests that fail when tenant behavior is accidental.

Required checks:

- persisted module entities are classified as `IScopedEntity`, `[GlobalEntity]`, `[DisableScopeFilter]`, or documented infrastructure exceptions;
- `IScopedEntity` EF entities have a `ScopeId` property with max length `ScopeIds.MaxLength`;
- `IScopedEntity` EF entities have the named `ScopeFilter`;
- no entity uses `[DisableScopeFilter]` with an empty reason;
- domain projects do not reference EF Core or ASP.NET;
- modules still do not reference other module internals;
- default hosts do not implicitly scan modules for tenancy.

If runtime inspection of EF metadata is practical, assert the actual model rather than relying only on source-text checks.

## Unit Tests

Add or update unit tests for:

- `ScopeIds` normalization remains the single reusable scope id rule;
- base scope aggregate/entity constructors normalize and reject invalid scope ids;
- EF helper applies filters only to `IScopedEntity` entities;
- `[GlobalEntity]` entities are not filtered;
- `[DisableScopeFilter]` requires a reason;
- write guard rejects missing, invalid, or mismatched scope ids;
- write guard allows matching scope ids;
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
