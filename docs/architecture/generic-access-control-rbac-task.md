# Generic Access Control And RBAC Refactor Task

Status: implemented in source-first skeleton; remote module packaging pending
Date: 2026-07-09

## Implementation Status

The framework and module extraction are implemented in the source-first skeleton:

- `Gma.Framework.AccessControl` now owns backend-neutral scope, requirement, decision, authorization service, decision-provider, grant-scope reader, scope-matching, and deny-by-default contracts.
- `Gma.Framework.Permissions` owns `PermissionCode` and module permission descriptors while preserving existing string `Code` accessors.
- `Gma.Framework.Administration` keeps source-compatible admin APIs, validates normal permission-code strings itself, and keeps the legacy `*` owner wildcard as an Administration compatibility concern.
- `Gma.Framework.Administration.AccessControl` is the explicit bridge from `IAdminAuthorizationService` to generic `IAccessAuthorizationService`.
- `Gma.Framework.AccessControl.AspNetCore` provides explicit `.RequirePermission(...)`, route/static/resolved-scope metadata, and filter-based enforcement.
- `Gma.Framework.Tenancy.AccessControl.AspNetCore` provides the optional tenant-scope HTTP bridge and `.RequireTenantPermission(...)` helpers.
- `Gma.Modules.AccessControl` owns persisted RBAC entities, SQL Server/PostgreSQL migrations, role/assignment application ports, bootstrap options, the persisted `IAccessDecisionProvider`, and the persisted `IAccessGrantScopeReader`.
- `Gma.Modules.Administration` owns audit persistence and empty admin API/CLI shell modules. `Gma.Modules.AccessControl.AdminCli` and `.AdminApi` own role management/bootstrap front doors.
- `Host.AdminCli`, `Host.AdminApi`, integration tests, solution layout, source roots, docs, and architecture guards compose AccessControl explicitly.
- Concrete persisted permission grants require exact scope matches. The owner wildcard is the only current grant that uses global/ancestor scope matching until descriptor-driven per-permission inheritance is implemented.
- Persisted permission checks prefilter by subject, relevant permission grants, and plausible request scopes in SQL before applying final scope-matching rules in memory.

Remote packaging follow-up:

- create the `GMA-Module-AccessControl` GitHub repository;
- replace the current skeleton folder with a real `gma/modules/access-control` submodule pinned to `dev`;
- add the submodule to `.gitmodules` and the submodule dev-head guard after the remote exists.

The remaining sections preserve the design record and next-step intent. They are not a second source of truth over the implementation status above.

Promote authorization from an Administration-owned concept into reusable GMA access-control infrastructure. Administration should become a management surface over generic access control, not the owner of RBAC semantics.

## Context

The first access-control slice deliberately kept `Gma.Framework.AccessControl` tiny. It only owns `AccessSubject` and `AccessSubjectKind`, while module-specific visibility rules remain in the owning domains.

That is still the right default for product-specific visibility and list filtering. However, product modules now need a repeated generic authorization shape for management-style actions across normal API hosts, admin API, admin CLI, workers, and automation:

- Auth identifies the subject.
- Access control decides whether the subject may perform a permissioned action in a scope.
- Product modules own deeper domain policy and resource filtering.
- Administration provides operator front doors for managing grants, roles, assignments, and audit.

Before this refactor, persisted RBAC lived in the Administration module through `AdminPermission`, `IAdminAuthorizationService`, `AdminOperationRunner`, and Administration persistence. That made sense for admin API/CLI, but it was too narrow for reusable product modules that need permission checks outside admin surfaces.

## Goal

Create a generic, optional GMA access-control/RBAC capability that can be reused by many projects and modules.

The target ownership model:

```text
Auth
  identifies subjects and manages sessions/tokens

Gma.Framework.AccessControl
  defines subject, scope, requirement, and decision contracts

Gma.Framework.AccessControl.AspNetCore
  adapts generic access requirements to endpoint authorization

Gma.Framework.Tenancy.AccessControl.AspNetCore
  adapts current tenant context to generic access scopes

Gma.Modules.AccessControl
  optionally persists RBAC roles, permissions, assignments, and grant state

Gma.Modules.Administration
  exposes admin API/CLI for managing AccessControl and auditing admin operations
```

The current Administration authorization APIs should remain source-compatible during the transition through adapters or wrapper types.

## Non-Goals

Do not implement:

- app-specific staff, property, department, housekeeping, accounting, or provider rules in GMA.
- A generic EF query-filter generator for business visibility.
- A generic relationship graph engine.
- OPA, Cedar, OpenFGA, SpiceDB, or other external policy engines in the core package.
- Permission claims inside JWTs as the default authorization source.
- Automatic hidden authorization over every endpoint or command.
- A broad rewrite of module-owned typed visibility scopes.

External policy engines and relationship stores can be optional adapters later if real deployments need them.

## Design Principles

- Code requires permissions, not roles. Roles are operator configuration; permissions are module contracts.
- Deny by default.
- Keep Auth identity-focused and authorization-focused storage outside Auth.
- Keep AccessControl core backend-free: no ASP.NET Core, EF Core, Redis, NATS, Auth module internals, or Administration module internals.
- Keep persisted RBAC optional and explicitly composed.
- Keep product/resource rules in the owning module.
- Make scope part of every decision so tenant-only grants can later evolve to tenant/property/resource grants.
- Do not cache allow/deny decisions until revocation and invalidation are documented.
- Prefer explicit endpoint/command metadata over hidden reflection.
- Preserve existing admin API/CLI behavior while moving the authorization source underneath it.

## Core Model

Framework-owned concepts:

```text
AccessSubject
  kind: user | admin-actor | service | system
  id: bounded stable identifier
  tenant id: optional current tenant context

Permission
  code: dotted lower-case permission code, for example example.resources.manage

AccessScope
  global
  tenant:<tenant-id>
  tenant:<tenant-id>/property:<property-id>
  tenant:<tenant-id>/property:<property-id>/department:<department-id>

AccessRequirement
  subject
  permission
  requested scope
  optional resource identity/metadata

AccessDecision
  allowed | denied | abstain
  reason code/message for bounded diagnostics
```

Initial scope matching should be simple and documented:

- exact scope grants satisfy exact scope requests;
- ancestor grants satisfy descendant requests when the permission descriptor allows inheritance;
- global grants satisfy all scopes only for permissions that explicitly permit global assignment;
- unknown or malformed scopes deny.

Initial provider pipeline behavior is also intentionally conservative:

- any deny decision vetoes prior or later allows;
- at least one allow is required;
- abstain-only or no-provider decisions deny by default.

Avoid product-specific segment names in the core. The core only validates segment names and values. Product modules decide whether `property`, `department`, `region`, or another segment has meaning.

## Framework Responsibilities

### `Gma.Framework.AccessControl`

Owns backend-neutral contracts and value objects:

- `AccessSubject` and `AccessSubjectKind` (already present);
- `AccessScope` and scope segments;
- `AccessRequirement`;
- `AccessDecision`;
- `IAccessAuthorizationService`;
- `IAccessDecisionProvider` or equivalent provider pipeline;
- default deny-all authorization service;
- bounded access error/reason codes;
- optional composition feature ids such as `access-control.authorization`.

It must not reference ASP.NET Core, EF Core, Auth, Administration, Tenancy runtime, Redis, NATS, or external policy engines.

### `Gma.Framework.Permissions`

This package currently owns module permission metadata. Decide whether to:

- keep it as the static metadata package and make descriptors use the new `Permission` type; or
- fold it into `Gma.Framework.AccessControl` if the package split no longer earns its keep.

Prefer the least disruptive route first: keep static module metadata separate, but align names and value objects.

Permission descriptors should eventually express:

- code;
- display description;
- allowed scope shape, such as global, tenant, or resource-scoped;
- whether ancestor scope grants inherit to descendants;
- whether the permission is admin-only, public-management, service-only, or general.

### `Gma.Framework.AccessControl.AspNetCore`

New optional web adapter package.

Owns:

- `RequirePermission(...)` endpoint helpers;
- claim-to-subject resolution;
- static, route, and named scope resolution helpers;
- endpoint metadata for architecture tests and OpenAPI enrichment;
- HTTP result mapping for unauthenticated, unauthorized, invalid scope, and missing scope resolvers.

Example target:

```csharp
resources.MapPost("/{resourceId:guid}/children", ...)
    .RequireRouteScopePermission(
        ProductPermissions.ChildrenManage,
        scopeSegmentName: "resource",
        routeValueName: "resourceId");
```

Tenant scope is deliberately outside this package. Compose `Gma.Framework.Tenancy.AccessControl.AspNetCore` when the endpoint wants current-tenant scope resolution:

```csharp
resources.MapPost("/", ...)
    .RequireTenant()
    .RequireTenantPermission(ProductPermissions.ResourcesManage);
```

Do not make this a hidden global endpoint filter. Route authors must add explicit metadata.

### `Gma.Framework.Administration`

Keep the operation-runner and admin-front-door contracts, but do not make the core Administration package depend on generic AccessControl.

Likely migration:

- `AdminPermission` validates compatible permission-code strings locally;
- `Gma.Framework.Administration.AccessControl` adapts `IAdminAuthorizationService` to `IAccessAuthorizationService`;
- the bridge builds an `AccessRequirement` from the admin actor, operation permission, and tenant scope;
- admin audit remains admin-operation audit, not every generic access decision.

This keeps CLI/API behavior stable while moving RBAC ownership out of Administration.

### Optional CQRS/Worker Adapters

Do not build these first unless a product module immediately needs them.

Future shape:

- command/query marker interfaces for access requirements;
- CQRS pipeline behavior that authorizes before handler execution;
- worker/service-principal helpers for scheduled tasks and data-provider adapters.

Endpoint checks are enough for the first real product module use case, but the core authorization service must be usable outside HTTP from day one.

## Module Responsibilities

### New `Gma.Modules.AccessControl` Or `Gma.Modules.Rbac`

Preferred name: `Gma.Modules.AccessControl`.

Reason: RBAC is the first persisted strategy, but the module may later own grant introspection, audit views, service-principal assignments, resource-scoped assignments, and adapter hooks that are broader than strict RBAC.

Owns optional persisted implementation:

- roles;
- role permissions;
- subject role assignments;
- assignment scopes;
- optional service-principal assignments;
- persisted authorization repository;
- SQL Server/PostgreSQL migrations;
- cache/invalidation strategy only after revocation semantics are documented;
- implementation of `IAccessDecisionProvider` or `IAccessAuthorizationService`.

Suggested tables:

```text
access_principals
access_roles
access_role_permissions
access_subject_role_assignments
access_audit_entries (optional, only if generic decision audit is explicitly enabled)
```

Important modeling choices:

- Store subject kind and subject id, not only a string actor id.
- Store assignment scope as normalized scope data, not loose tenant id columns only.
- Keep permission codes as stable strings declared by modules.
- Keep role names as operator-facing slugs.
- Keep wildcard grants explicit and narrowly documented. Consider replacing the current `*` owner wildcard with descriptor-driven owner roles or a named `access.owner` convention before exposing generic wildcard behavior.

### `Gma.Modules.Administration`

After the refactor, Administration owns audit storage and shell composition points, not RBAC storage semantics or role-management front doors.

It should:

- expose admin audit views when useful;
- continue to run future admin audit operations through admin operation authorization and audit;
- avoid AccessControl module dependencies unless a future audit feature truly needs them.

It should not:

- define generic permission, role, assignment, or scope value objects;
- bootstrap the first owner;
- expose role creation, permission grant, or role assignment commands/routes;
- be required by normal API hosts that only need to enforce permissions;
- be the only place persisted RBAC can be composed.

### Auth Module

Auth remains identity/session infrastructure.

It should not:

- persist roles;
- issue role/permission claims by default;
- decide access to product resources;
- reference AccessControl persistence.

It may expose subject identifiers and tenant claims that AccessControl adapters can consume.

### Product Modules

Product modules should:

- declare permission codes in contracts/metadata;
- use generic permission checks for management-style actions;
- use `IAccessGrantScopeReader` for bulk authorization inputs when access-control grants drive list filtering, then translate the returned scopes through module-owned query scopes;
- keep resource-specific visibility and list filtering in module-owned policies and typed scopes;
- avoid hard-coding role names.

Example:

```text
Example product module
  declares example.items.manage
  uses RequirePermission for create/update/retire room actions
  keeps product-specific assignment meaning outside generic GMA core
```

## Scope Boundaries

Generic AccessControl answers:

```text
Can subject S perform permission P in scope X?
```

Module policy answers:

```text
What does scope X mean for this domain?
Which resources are visible for this use case?
Which resource-state transitions are valid?
```

Examples:

- AccessControl can allow `example.resources.manage` for `tenant:a/resource:r1`.
- A product module decides whether `p1` exists, whether the resource is retired, and whether a typed list scope translates into SQL.
- Staff-like modules decide whether a subject is assigned to a resource.
- Workflow modules decide whether a state transition is valid.

Do not put those product meanings into the generic RBAC store.

## Endpoint Enforcement

First useful slice:

- Add `.RequirePermission(...)` for minimal APIs.
- Make it use `IAccessAuthorizationService`.
- Resolve subject from authenticated claims.
- Resolve tenant scope from `ITenantContext` or configured tenant header/claim.
- Return `401` when no authenticated subject exists.
- Return `403` when access is denied.
- Return `400` or `403` for invalid/mismatched scope depending on whether the input is malformed or authenticated but forbidden.
- Attach endpoint metadata so architecture tests can find protected routes.

The helper must be explicit at route/group level.

## Admin Compatibility Flow

Existing flow:

```text
AdminApiExecutor/AdminCliExecutor
  -> AdminOperationRunner
  -> IAdminAuthorizationService
  -> Administration persisted RBAC
```

Target flow:

```text
AdminApiExecutor/AdminCliExecutor
  -> AdminOperationRunner
  -> IAccessAuthorizationService adapter
  -> AccessControl persisted RBAC
```

Compatibility rules:

- Existing admin modules should compile with minimal changes.
- Existing permission code strings should remain stable.
- Existing owner bootstrap should still create an operator with full administrative permissions.
- Existing admin audit shape can stay in Administration until a generic audit reason appears.

## Migration Plan

### Phase 1: Framework Contracts

- Add generic permission, scope, requirement, decision, and authorization service contracts.
- Add tests for normalization, invalid inputs, scope matching, and deny-by-default behavior.
- Keep current `AdminPermission` and `IAdminAuthorizationService` source-compatible while moving AccessControl adaptation to the explicit bridge package.
- Add docs and architecture guards that keep the core package backend-free.

### Phase 2: ASP.NET Adapter

- Add `Gma.Framework.AccessControl.AspNetCore`.
- Implement explicit `.RequirePermission(...)`.
- Add focused endpoint tests for authenticated allowed, authenticated denied, unauthenticated, tenant mismatch, and invalid scope.
- Do not require Administration module for the endpoint helper unless a persisted RBAC provider is selected.

### Phase 3: Persisted RBAC Module

- Create `Gma.Modules.AccessControl`.
- Move or duplicate the persisted RBAC model out of `Gma.Modules.Administration`.
- Add migrations for supported providers.
- Add an `IAccessAuthorizationService` implementation backed by persisted assignments.
- Decide migration compatibility for existing Administration tables.

### Phase 4: Administration Rewire

- Move role-management API/CLI surfaces to AccessControl admin front doors.
- Keep Administration application/API/CLI audit-focused.
- Keep admin operation audit behavior stable.
- Preserve CLI command names where possible.
- Add integration tests proving old admin flows still authorize module admin operations.

### Phase 5: Product Module Adoption

- Apply endpoint permission checks in the first product module that needs management permissions.
- Add app/module architecture tests for state-changing management endpoints requiring tenant and permission metadata.
- Keep product-domain policies focused on product rules, not RBAC semantics.

### Phase 6: Optional Command/Worker Enforcement

- Add command/query authorization metadata only after repeated need.
- Add service-principal helpers for workers/data-provider adapters when real adapter modules need them.

## Resolved Decisions And Follow-Ups

- Resolved: the persisted module is named `Gma.Modules.AccessControl`, because RBAC is the first strategy but not the whole concept.
- Resolved: `Gma.Framework.Permissions` remains a static metadata package and normalizes through `PermissionCode`.
- Resolved: `AccessScope` is a typed value object backed by a normalized string path and segment parser.
- Resolved: global and ancestor scope grants are explicit `AccessScopeMatchOptions`, not automatic defaults.
- Resolved: wildcard owner grants stay as an Administration compatibility concern for now.
- Resolved: admin-to-access-control authorization lives in `Gma.Framework.Administration.AccessControl`, not core `Gma.Framework.Administration`.
- Resolved: persisted RBAC keeps concrete permission grants exact-scope by default; descriptor-driven inheritance remains a later extension point.
- Resolved: endpoint helpers use explicit endpoint filters and metadata.
- Resolved: Administration RBAC migrations are preserved as compatibility/no-op migrations while AccessControl owns new RBAC schema migrations.
- Follow-up: generic decision audit remains deferred until a real non-admin audit use case appears.
- Follow-up: command/query/worker authorization metadata remains deferred until repeated use proves the extra package surface is worth it.

## Acceptance Checks

Before calling the source-first implementation done:

- `Gma.Framework.AccessControl` has no ASP.NET Core, EF Core, Auth, Administration, Redis, NATS, or external policy engine references.
- Generic permission and scope value objects have focused tests.
- Deny-by-default works without a persisted RBAC module.
- Administration API/CLI authorization behavior remains covered by tests.
- Persisted RBAC is optional and explicitly composed.
- Normal API endpoints can require permissions without registering admin API/CLI modules.
- Module permission descriptors stay stable for existing modules.
- Product modules can use generic permission checks for management writes without depending on Administration.
- Docs clearly state which rules belong in framework, AccessControl module, Administration module, Auth, and product modules.

## Product Driver Example

A real product module may need permission checks on tenant-scoped management endpoints in the normal API host. That is the intended first adopter pattern.

Example target after the GMA refactor:

```csharp
resources.MapPost("/", ...)
    .RequireTenant()
    .RequirePermission(ProductPermissions.ResourcesManage);

resources.MapPost("/{resourceId:guid}/children", ...)
    .RequireTenant()
    .RequirePermission(ProductPermissions.ChildrenManage);

resources.MapPost("/children/{childId:guid}/nested-children", ...)
    .RequireTenant()
    .RequirePermission(ProductPermissions.NestedChildrenManage);
```

Resource-scoped grants can come later when a product module defines real requirements.
