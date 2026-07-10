# Scoping And Tenancy

Scoping is the neutral runtime/storage isolation layer. Tenancy is the product-facing tenant concept. Keep them separate so reusable GMA modules can support tenant and non-tenant hosts without taking a hard dependency on the Tenancy package.

## Responsibilities

`Gma.Framework.Scoping` owns:

- `IScopeContext` and `IScopeContextAccessor`;
- `ScopeIds` validation and normalization;
- `ScopeCompositionFeatures.Context`;
- `[ScopeAware]` metadata for contracts that need isolation without tenant semantics;
- `RequireScope()` for endpoints that need a configured active scope when scoping is enabled.

`Gma.Framework.Tenancy` owns:

- `ITenantContext` and `ITenantContextAccessor`;
- tenant header/claim semantics;
- `[TenantScoped]` metadata and tenancy composition features;
- tenant-specific bridges for CQRS, caching, messaging, tasks, logging, and access control.

`Gma.Framework.Tenancy.Scoping` is the adapter between them. It provides `IScopeContext` from the active `ITenantContext` when tenancy is enabled, and falls back to `ScopeOptions.LocalDefaultScopeId` for non-tenant/global hosts.

## Module Rule

Reusable modules that only need runtime or storage isolation should depend on Scoping, not Tenancy. Their profiles should require `scoping.context`, and their code should accept `IScopeContext`.

Modules or adapters that truly need tenant semantics should depend on Tenancy. Examples include tenant headers, tenant claims, tenant-aware cache scopes, tenant-owned task leases, tenant-specific access policy, and tenant integration-event consumer context.

Application-owned modules may use Tenancy directly when the product domain is tenant-shaped. The reusable framework and first-party reusable modules should keep Tenancy behind explicit bridge packages unless tenant semantics are the actual feature.

## Host Shapes

Tenant-aware host:

```csharp
builder.AddGmaInfrastructure();
builder.AddModule<TenancyModule>();
builder.AddAuthModule(AuthProfile.ScopeAware());
builder.ValidateModuleComposition();
```

`AddGmaInfrastructure()` composes Scoping, Tenancy, and the Tenancy-to-Scoping bridge. `TenancyModule` adds HTTP tenant header resolution.

Tenant-free/global host:

```csharp
builder.AddGmaInfrastructure();
builder.AddAuthModule(AuthProfile.Global("global"));
builder.ValidateModuleComposition();
```

The global profile configures `ScopeOptions.LocalDefaultScopeId` and does not require a tenant module.

## Guardrails

- Do not grow Scoping into Tenancy 2. Scoping should know only whether a scope is enabled and what the active scope id is.
- Do not put header names, claims, authorization policy, tenant lifecycle, or tenant catalog behavior into Scoping.
- Keep storage columns stable unless a real migration is intended. Generic infrastructure rows may use `ScopeId` in code while mapping to an existing `TenantId` column for compatibility.
- Prefer bridge packages over direct dependencies when a cross-cutting capability can be tenant-aware optionally.
