# Shared Access Subject Foundation

## Decision

Keep `Gma.Framework.AccessControl` as a backend-agnostic access vocabulary and decision pipeline for coarse permission checks.

The package owns only the common vocabulary needed to pass the current actor across module boundaries:

```csharp
public enum AccessSubjectKind
{
    Unknown = 0,
    User = 1,
    AdminActor = 2,
    Service = 3,
    System = 4
}

public sealed record AccessSubject(
    AccessSubjectKind Kind,
    string Id);
```

It normalizes subject ids, rejects unknown subject kinds, and stays free of HTTP, EF, Auth, Administration, Tenancy runtime, NATS, Redis, and external policy engines.

The same package also owns the generic coarse authorization vocabulary:

- `PermissionCode`;
- `AccessScope` and `AccessScopeSegment`;
- `AccessRequirement`;
- `AccessDecision`;
- `IAccessAuthorizationService`;
- `IAccessDecisionProvider`.

Permission metadata for module descriptors lives in `Gma.Framework.Permissions`. HTTP enforcement lives in `Gma.Framework.AccessControl.AspNetCore`. Tenant scope resolution lives in `Gma.Framework.Tenancy.AccessControl.AspNetCore`.

## Why

The first access-policy slice proved that `AccessSubject` is useful: API endpoints, CLI/admin flows, workers, and tests can pass a stable actor object without leaking `ClaimsPrincipal`, auth schemes, or raw claim parsing into application handlers.

The generic policy/evaluator layer only earns its keep for coarse permissioned operations: "can this subject perform permission P in scope X?" It must not replace module-owned business visibility rules. The important list/detail protection still comes from module-owned typed scopes that persistence must consume.

## Current Pattern

For product/resource reads:

```text
front door
  -> build AccessSubject and module-specific input
  -> application adapts input to module domain objects
  -> domain visibility policy returns a typed scope
  -> repository requires that scope
  -> persistence translates scope into SQL/read-model filters
```

For simple application-only checks that do not shape persistence, use direct code in the owning module. Use generic access-control requirements for repeated management-style operations, API endpoint checks, admin authorization bridges, workers, or automation.

## Module Ownership

The module that owns the resource owns the access language.

Good examples:

- Catalog owns region availability and returns `AvailableCatalogItemsScope`.
- Ordering owns current-user order visibility and returns `UserOrdersScope`.
- Notifications stores already-addressed notifications and performs simple current-user checks directly in application code.

Shared code must not know product concepts such as friend, blocked user, manager, HR, viewer, editor, catalog region, notification recipient, or order owner.

## Tenant Handling

`AccessSubject` is intentionally identity-only. Do not put tenant ids on it.

Tenant isolation remains separate from resource visibility:

- tenant filters prevent cross-tenant data leaks;
- module visibility scopes decide which resources are visible inside an allowed tenant or across explicitly global/platform resources.
- generic permission checks receive tenant context as an `AccessScope` such as `tenant:default`;
- HTTP tenant-scope resolution is provided by `Gma.Framework.Tenancy.AccessControl.AspNetCore`, not by the access-control core.

## Follow-Up Direction

The first slice stopped at `AccessSubject` because generic grants had not yet earned their keep. A later product-management use case did prove repeated need for permissioned actions outside admin-only surfaces. See [Generic Access Control And RBAC Refactor Task](generic-access-control-rbac-task.md) for the implemented persisted RBAC module and framework bridge.

## Future Options

Add relationship/object-sharing models only when several modules need the same shape.

Possible future model:

```text
subject kind/id
resource module/type/id/scope
relation or level
created by/at
expires at
source
```

External engines such as OPA, Cedar, OpenFGA, or SpiceDB remain optional adapters for concrete deployment needs. They should live outside the core subject package.

## Guardrails

- Do not add automatic endpoint filters that make authorization invisible.
- Do not add a generic EF query-filter builder for all modules.
- Do not cache allow/deny decisions unless the owning module documents revocation and invalidation.
- Do not put tenant ids, user ids, resource ids, subject ids, or policy input values in metric tags.
- Use not-found-shaped failures for private single-resource access when a forbidden response would reveal existence.
- Keep list/search/feed/export/stream reads scope-aware in repositories, projections, or read models.

## Tests

- `Gma.Framework.AccessControl` tests cover subject, scope, decision, provider-order, and deny-by-default behavior.
- Module tests cover each domain visibility policy or direct application access check.
- Persistence tests prove typed scopes translate into query filters.
- Architecture tests keep the shared subject package backend-free and keep external access adapters out of domain projects.
