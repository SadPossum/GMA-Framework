# Tenant Model Conventions Implementation Notes

Temporary working notes for the tenant model conventions refactor.

## Decisions

- Keep tenant ownership explicit in models through `ITenantScoped`, `TenantAggregateRoot<TId>`, or `TenantEntity<TId>`.
- Do not add shadow `TenantId` properties. Tenant id remains domain/application data, not a host-side persistence trick.
- Centralize EF tenant property and `TenantFilter` configuration in `Gma.Framework.Persistence.EntityFrameworkCore`.
- Use a `TenantAwareDbContext<TContext>` base so filters can reference context-instance tenant values and write guards run for every `SaveChanges`.
- Keep infrastructure records conservative. Outbox/inbox/task/audit/projection rows may contain tenant ids without being tenant-owned domain entities.

## Initial Classification

- Auth tenant-owned: `Member`, `MemberUsername`, `MemberSession`.
- Catalog tenant-owned: `CatalogItem`.
- Ordering tenant-owned: `Order`, `CatalogItemProjection`, `OrderingProjectionRebuildCheckpoint`.
- Messaging runtime rows: tenant-associated, not tenant-filtered by default because publishers/consumers need cross-tenant runtime visibility.
- Administration RBAC/audit: control-plane data, not part of this first refactor slice.
- TaskRuntime: control-plane/runtime data, not part of this first refactor slice.

## Audit Items

- Verify convention filters are dynamic per `DbContext` instance, not frozen from the first built EF model.
- Verify write guard fails closed for missing, invalid, unnormalized, and mismatched tenant ids.
- Verify migrations stay clean for Auth/Catalog/Ordering because schema shape should not change.
