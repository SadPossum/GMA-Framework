# Method-Aware HTTP Rate Limiting Task

Status: completed
Date: 2026-08-12

## Goal

Allow applications to protect distinct HTTP operation classes without forcing
read-only traffic and mutations beneath one path-wide client-IP budget. Preserve
the existing production HTTP defaults and the atomic behavior of in-process and
distributed providers.

## Ownership

- `Gma.Framework.Api.Production` owns configuration, validation, request
  matching, middleware behavior, and HTTP problem responses.
- `Gma.Framework.RateLimiting` continues to own only provider-neutral atomic
  fixed-window primitives. It receives no HTTP, identity, or product semantics.
- Applications own policy names, paths, methods, permit counts, capacity
  decisions, and deployment evidence.

## Contract

1. The existing global client budget always applies.
2. Existing `SensitivePathPrefixes` remain backward compatible and match every
   method beneath those paths.
3. Applications may add a bounded set of named policies. A policy defines one
   permit limit, one or more absolute path prefixes, and optional standard HTTP
   methods. Empty methods mean all methods.
4. Every matching policy is consumed with the global budget as one decision in
   distributed mode. In-process mode applies behaviorally equivalent chained
   limiters.
5. Policy names are stable storage identities, unique, normalized, and safe for
   metrics or provider keys. A request consumes a policy at most once even when
   several prefixes match.
6. Rejections expose the real provider or limiter retry interval when available;
   the configured window is only a bounded fallback.
7. Client identity remains the trusted resolved remote address. The framework
   must not partition on an unvalidated bearer token or cookie. Subject-aware
   fairness requires a separate explicit post-authentication design.

## Delivery

1. Add policy options and fail-fast validation with compatibility guards.
2. Share one matcher between in-process and distributed admission paths.
3. Cover method matching, multiple atomic policies, legacy behavior, invalid
   configuration, and retry guidance with focused tests.
4. Document the new configuration and update the production-readiness ledger.
5. Sync the framework revision into GMA Skeleton and prove one application-owned
   policy composition without adding application paths to framework defaults.

## Done When

- existing consumers behave unchanged without new configuration;
- configured GET polling can be excluded from mutation budgets while the same
  path's POST remains protected;
- overlapping policies are enforced atomically and never partially consumed;
- invalid names, methods, paths, counts, or policy cardinality fail startup; and
- framework, Skeleton, and adopter tests pass at the same framework revision.

## Outcome

GMA now supports up to seven named method-aware policies alongside the legacy
sensitive-path budget. Both in-process and distributed modes use the same
segment-aware matcher; distributed requests consume every matching policy and
the global budget atomically. Startup rejects unsafe names, paths, methods,
limits, duplicates, and policy sets that exceed the provider's eight-partition
contract. In-process rejection uses limiter retry metadata when available.

The complete `Gma.Framework.Tests` suite passed 1,133 tests. GMA Skeleton's
selection matrix generated and security-checked every module combination, then
built the all-admin fixture with zero warnings or errors. BunkFy's focused
composition guard passed against the same framework revision. Deployed capacity
evidence remains application-owned.
