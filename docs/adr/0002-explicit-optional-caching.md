# ADR 0002: Explicit Optional Caching

## Status

Accepted

## Date

2026-06-23

## Context

The skeleton needs reusable caching without making cache availability part of domain correctness or coupling modules to Redis. Automatic CQRS query caching would hide key construction, tenancy, failure-result policy, and invalidation timing behind marker interfaces.

## Decision

Use explicit cache-aside reads through `IApplicationCache` in `Gma.Framework.Caching`.

Use .NET HybridCache in `Gma.Framework.Caching.Infrastructure` for memory caching, serialization, and stampede protection. Keep Redis in the separately referenced `Gma.Framework.Caching.Redis` adapter, tenant cache scope resolution in `Gma.Framework.Tenancy.Caching`, and CQRS post-commit invalidation wiring in `Gma.Framework.Caching.Cqrs`. Caching is disabled by default, Redis and tenant scope resolution are host opt-in, and runtime backend failures fail open.

Commands enqueue key or tag invalidations. A command pipeline behavior flushes them only after the unit-of-work behavior commits successfully.

Tenant/global logical key types are mandatory. Infrastructure owns physical key construction and observability. Tenant-scoped cache keys fail fast unless the tenant cache bridge is composed.

## Consequences

Positive:

- modules remain provider-independent;
- projects that do not need caching pay little operational cost;
- cache usage is visible at each read site;
- invalidation cannot run before a successful commit;
- Redis can be added without changing module code.

Negative:

- handlers contain explicit cache-aside code;
- invalidation choices remain an application design responsibility;
- cross-node L1 invalidation is eventually consistent;
- fail-open behavior can temporarily increase source load during outages.

## Alternatives Considered

- Automatic `ICacheableQuery` behavior: rejected for now because it hides result-caching and invalidation policy.
- Redis contracts in modules: rejected because it couples application code to infrastructure.
- Cache required by default: rejected because caching is an optimization, not a correctness dependency.
- Synchronous invalidation inside command handlers: rejected because it can invalidate before database commit.
