# Shared Access Policy Implementation Notes

These notes capture the first implementation slice for `src/Framework/docs/architecture/shared-access-policy-foundation.md`.

## Implemented Slice

- Added `Gma.Framework.AccessControl` as a small backend-agnostic shared project.
- Added validated access primitives for front-door subjects only.
- Removed the generic policy/evaluator layer after the first slice showed it was wrapping simple static checks without enough payoff.
- Notifications now performs direct current-user notification history checks in application code.
- Adopted the foundation in the optional Catalog example for region-scoped item availability reads.
- Adopted the foundation in the optional Ordering example for current-user order list/detail access and user-scoped order placement.
- Added unit tests for subject validation.
- Added Notifications persistence/application tests for wrong-user and wrong-tenant current-user history access.
- Added architecture guards to keep `Gma.Framework.AccessControl` free of HTTP, EF, admin, hosting, NATS, and external policy-engine dependencies.

## Design Decisions

- The core package stays independent from ASP.NET Core, EF, Auth, Administration, Tenancy runtime, NATS, Redis, OpenFGA, SpiceDB, OPA, Cedar, and similar engines.
- Access subjects are constructed explicitly at front doors, workers, or tests; the core package does not parse claims or resolve tenants.
- Requirements, policies, actors, and access scopes belong to the module that owns the resource. Business visibility rules should live in domain code when they are part of product behavior.
- Administration RBAC remains separate. Admin authorization still goes through the existing admin executors and audit pipeline.
- Single-resource private access denial can be shaped as not-found by the owning module to avoid leaking resource existence.
- List, feed, stream, search, and export paths should use domain-produced access scopes that repositories, projections, or read models must consume rather than broad in-memory filtering.

## Notifications Adoption

Notifications uses only the shared subject vocabulary for current-user history. `Gma.Modules.Notifications.Api` resolves the authenticated user into an `AccessSubject.User(...)` and passes that subject to application commands and queries. `Gma.Modules.Notifications.Application.Visibility.NotificationHistoryAccess` performs the simple user/tenant checks directly, while `Gma.Modules.Notifications.Persistence` keeps list/stream/mark-all visibility constrained by subject-scoped queries.

## Catalog And Ordering Proof Of Concept

Catalog uses domain-owned scoped access for regional availability:

- `CatalogViewer`
- `CatalogAvailabilityPolicy`
- `AvailableCatalogItemsScope`
- `ApplyAvailableCatalogItemsScope(...)`

The user-facing Catalog API constructs an `AccessSubject.User(...)` from claims and tenant context, reads the allowed user region from the Catalog-owned `catalog_region` claim, and the application maps that input into `Catalog.Domain.Visibility` objects. The domain policy returns an `AvailableCatalogItemsScope` only when the requested region matches the viewer's allowed region. Protected repository methods require that scope, and persistence translates it into tenant, active-item, and item-availability filters in one query. Empty item region lists mean globally available; non-empty lists are normalized region codes.

Ordering now uses domain-owned scoped access for current-user order visibility:

- `OrderViewer`
- `OrderingVisibilityPolicy`
- `UserOrdersScope`
- `ApplyUserOrdersScope(...)`

Ordering stores the user id and region on the order. Current-user order list/detail repository methods require `UserOrdersScope`, and persistence translates the scope into tenant and user filters. Ordering consumes Catalog item events into a local projection that duplicates availability regions, rejects order placement when the item is unavailable in the requested region, and publishes addressed notification requests only for users with affected orders. Notifications does not know why a user can see a message.

## Audit Notes

- Keep the shared package small until a second or third module proves additional common shape.
- Do not add generic persisted ACL/grant tables until repeated module needs justify them.
- If an external policy engine is introduced later, add it as an optional adapter outside `Gma.Framework.AccessControl`.
- If admin/resource policy bridging is useful later, implement it as an adapter that wraps existing admin authorization; do not replace the admin operation runner.
- Avoid caching allow decisions unless the owning module documents revocation and invalidation.
- Repeated API subject construction is now visible in Notifications, Catalog, and Ordering. A future optional `Gma.Framework.AccessControl.Api` helper could reduce this front-door boilerplate without moving ASP.NET Core into the core package.
- Repeated single-resource not-found shaping and scoped repository patterns are good template material, but the scope types and query translators should stay module-owned until more examples prove a safe abstraction.
- Region-code rules are duplicated in Catalog contracts/domain and Ordering domain. Keep them module-local unless another module needs the exact same semantics; then consider an optional geography primitives package.
