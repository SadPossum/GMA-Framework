# ADR 0004: Optional Administration API

## Status

Accepted

## Date

2026-06-29

## Context

The administration framework started as CLI-first so public API deployments would not automatically expose operator surfaces. The skeleton now needs an HTTP administration option for projects that want machine-to-machine admin automation, internal tools, or a future admin UI.

The same modularity rules still apply:

- public API composition stays separate from admin composition;
- modules are registered explicitly;
- RBAC, audit, and tenant scope stay behind shared administration contracts;
- feature admin endpoints reuse application commands and queries, not persistence internals.

## Decision

Add administration HTTP APIs through a separate optional host and adapter:

- `Gma.Framework.Administration.Api` contains generic admin API composition, actor resolution, and result mapping.
- `Host.AdminApi` composes only the admin API modules a deployment wants.
- `Gma.Modules.Administration.AdminApi` exposes RBAC role management endpoints.
- `Gma.Modules.Auth.AdminApi` exposes Auth member management endpoints.
- `Host.Api` does not register admin API modules.
- Tenant-scoped admin API calls use the configured tenant header plus optional token tenant binding. If the configured token tenant claim is present, it must match the requested tenant; if absent, RBAC remains the tenant authority.

The admin API adapter shares `IAdminOperationRunner` with the CLI, so authorization, tenant checks, audit records, and operation result semantics stay consistent across admin surfaces.

## Consequences

Positive:

- Admin HTTP is available without making it part of the public API host.
- CLI and HTTP admin paths use the same authorization and audit behavior.
- Feature modules can expose admin APIs through `<Module>.AdminApi` without depending on ASP.NET from domain or application code.
- Architecture tests can enforce API/CLI dependency isolation.
- External identity providers can issue global operator tokens without a tenant claim while tenant-bound tokens still get confusion protection.

Negative:

- Projects that enable admin HTTP must deploy and secure another host.
- Bootstrap remains CLI-first; a fresh environment still needs a controlled owner setup path.
- Admin API contracts need the same care as public contracts because they can be automated by external tools.
- Deployments that disable tenant-claim matching must rely on persisted RBAC and gateway policy for tenant confusion protection.

## Alternatives Considered

- Add admin routes to `Host.Api`: rejected because it makes public deployments carry admin surface area by default.
- Add admin endpoints directly to feature API modules: rejected because it mixes public and operator concerns.
- Build an admin UI first: deferred until real product workflows justify it.
