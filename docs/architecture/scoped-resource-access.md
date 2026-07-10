# Scoped Resource Access

## Decision

Use domain-owned access policies that return typed access scopes for list, search, feed, export, and other multi-resource reads.

`Gma.Framework.AccessControl` provides front-door subject vocabulary and generic coarse permission decisions. It must not own business rules, EF translation, relationship models, product-specific policy evaluators, or external policy-engine dependencies.

The default pattern is:

```text
front door
  -> build AccessSubject and module-specific actor input
  -> application maps input to a domain actor
  -> domain policy returns a typed access scope
  -> repository requires that scope
  -> persistence translates scope into SQL/read-model filters
```

## Why

Boolean authorization is useful for some single-resource operations, but it is not enough for list-style reads. A handler-side `if allowed` check can still be followed by a broad repository query. That is both inefficient and leak-prone.

Production authorization systems solve this with data filtering. Oso describes list filtering as retrieving only resources a user can access in one operation instead of checking every resource individually. OPA frames data filtering as the authorization case that goes beyond allow/deny for search and listing. ASP.NET Core resource authorization is imperative and often happens after loading a resource, so it is not enough on its own for lists. EF global filters are still useful for universal constraints such as tenant isolation and soft delete, but business visibility usually needs per-use-case scope.

Reference points:

- [ASP.NET Core resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based)
- [EF Core global query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [EF Core multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy)
- [Oso list filtering](https://www.osohq.com/docs/develop/enforce/list-filtering)
- [OPA data filtering](https://openpolicyagent.org/docs/filtering)
- [OpenFGA List Objects](https://openfga.dev/docs/getting-started/perform-list-objects)

## Package Responsibilities

`Gma.Framework.AccessControl` may provide:

- `AccessSubject` and `AccessSubjectKind` for API, admin, CLI, worker, and test front doors;
- `PermissionCode`, `AccessScope`, `AccessRequirement`, `AccessDecision`, and provider-based authorization for coarse operation checks;
- future optional adapters, if they are separate packages.

`Gma.Framework.AccessControl` must not provide:

- Catalog, Ordering, Notifications, social graph, PMS, chat, report, or staff-management rules;
- generic EF query generation for business visibility;
- product-specific persisted grants or relationship graphs;
- external policy engine dependencies in the core package.

## Module Pattern

For access-controlled reads, place the rule language in the owning domain:

```text
<Module>.Domain/
  Visibility/
    <Module>Actor.cs
    <Operation>Scope.cs
    <Module><Area>Policy.cs
```

The policy returns a `Result<TScope>`:

```csharp
Result<AvailableCatalogItemsScope> scope =
    CatalogAvailabilityPolicy.CanViewAvailableItems(viewer, requestedRegion);
```

The application layer translates front-door input into the domain actor and calls the policy:

```text
AccessSubject + claim/header/query inputs
  -> CatalogViewer
  -> AvailableCatalogItemsScope
  -> ICatalogItemReadRepository.ListAvailableAsync(scope, page, ct)
```

The repository port should require the typed scope:

```csharp
Task<CatalogItemListResponse> ListAvailableAsync(
    AvailableCatalogItemsScope scope,
    PageRequest pageRequest,
    CancellationToken cancellationToken);
```

Do not expose parallel overloads that accept loose ids, tenant ids, regions, roles, or booleans for the same protected read path. The point of the scope is to make bypasses awkward.

## Persistence Pattern

Persistence translates the domain scope into provider-translatable queries:

```text
<Module>.Persistence/
  QueryScopes/
    <Aggregate>AccessScopeExtensions.cs
```

Example shape:

```csharp
internal static IQueryable<CatalogItem> ApplyAvailableCatalogItemsScope(
    this IQueryable<CatalogItem> query,
    AvailableCatalogItemsScope scope) =>
    query.Where(item =>
        item.TenantId == scope.TenantId &&
        item.Status == CatalogItemState.Active &&
        (!item.AvailableRegions.Any() ||
         item.AvailableRegions.Any(region => region.Region == scope.Region)));
```

For relationship-heavy modules, the translation can use `JOIN`, `EXISTS`, local projections, precomputed feed tables, or bounded ID sets. Avoid loading broad data and filtering in memory.

## Single-Resource Reads

Single-resource reads still need care.

Good options:

- query by id through the same scope and return not-found-shaped failures for private resources;
- load a minimal access summary, run a domain policy, then load/mutate only if allowed;
- use direct application checks for simple operational rules that do not affect list filtering.

Avoid loading the full private aggregate before authorization unless the module has explicitly accepted that tradeoff.

## Cache Rules

Cache keys must include every scope dimension that can change the visible data set.

Examples:

- tenant-owned reads should use tenant-scoped cache keys;
- region-scoped Catalog reads include normalized region;
- role/grant/projection-scoped reads need documented invalidation before caching.

Do not cache allow/deny decisions unless revocation and invalidation are documented.

## Cross-Module Rules

The module that owns the resource owns its access policy and persistence translation.

Examples:

- Chat owns room membership and computes addressed notification recipients; Notifications stores and streams already-addressed notifications.
- PMS owns manager/property policy and joins against PMS-owned assignments or projections.
- Catalog owns item regional availability; Ordering duplicates the Catalog item projection it needs for order decisions.

Cross-module synchronous policy calls are not the default. Prefer contracts, local projections, integration events, and module-owned duplicated read data.

## Tests

Every module using scoped resource access should have:

- domain tests for actor creation, denied cases, and returned scopes;
- application tests proving denied scopes stop before repository calls when practical;
- persistence tests proving the scope is translated into query filters;
- endpoint or integration tests for one list path and one single-resource path;
- cache tests or docs proving cache keys include scope dimensions;
- architecture tests if the module introduces new package references or adapters.

## Catalog Example

Catalog demonstrates the default pattern:

- `Catalog.Domain.Visibility.CatalogViewer` models the Catalog-visible product user after the application adapts front-door identity and claim inputs.
- `CatalogAvailabilityPolicy` returns `AvailableCatalogItemsScope` only when the requested region matches the viewer's Catalog-owned region.
- `ICatalogItemReadRepository.GetAvailableAsync(...)` and `ListAvailableAsync(...)` require `AvailableCatalogItemsScope`.
- `Catalog.Persistence.QueryScopes.ApplyAvailableCatalogItemsScope(...)` applies tenant, active-status, and region availability filters in SQL.
- API and application code never manually construct EF predicates for this policy.

## Ordering Example

Ordering demonstrates the same pattern for ownership-style reads:

- `Ordering.Domain.Visibility.OrderViewer` models the current product user inside a tenant.
- `OrderingVisibilityPolicy` returns `UserOrdersScope` for the user's own order history.
- `IOrderReadRepository.GetAsync(...)` and `ListAsync(...)` require `UserOrdersScope`.
- `Ordering.Persistence.QueryScopes.ApplyUserOrdersScope(...)` applies tenant and user filters in SQL.

Ordering also demonstrates cross-module data ownership: it stores Catalog item ids and duplicated Catalog projection data locally, but order visibility remains an Ordering policy.
