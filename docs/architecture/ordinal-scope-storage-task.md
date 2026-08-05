# Ordinal Scope Storage Task

Status: complete
Date: 2026-08-05

## Goal

Keep case-preserving scope identifiers isolated with the same ordinal equality
semantics on PostgreSQL and SQL Server without transforming external ids or
making indexed scope queries non-sargable.

## Contract

- `ScopeIds` remains trimmed and case-preserving; `Tenant-A` and `tenant-a` are
  distinct scope identifiers.
- PostgreSQL uses its deterministic, case-sensitive text equality defaults.
- SQL Server `ScopeId` columns use `Latin1_General_100_BIN2`, overriding common
  case-insensitive database defaults at the column boundary.
- `ApplyScopeConventions(...)` applies ordinal storage to every mapped string
  `ScopeId`, including infrastructure records that are deliberately not read
  filtered.
- Plain infrastructure DbContexts that do not inherit `ScopeAwareDbContext`
  call `ApplyOrdinalScopeIdConventions(...)` after registering mappings.

## Guardrails

- Do not lowercase or otherwise rewrite externally supplied identifiers.
- Do not add query-level `COLLATE`, `ToLower`, or `ToUpper` operations; those can
  prevent scope indexes from serving equality filters.
- Product tenant admission and lifecycle policy remain outside Framework.
- Provider-specific schema changes remain in provider migration projects.

## Delivery

1. [x] Add the reusable Framework ordinal scope-id model convention.
2. [x] Apply it automatically from scope-aware persistence conventions.
3. [x] Replace Task Runtime's local SQL Server scope collation wiring.
4. [x] Add provider migrations for affected reusable modules.
5. [x] Verify case-distinct scope behavior against PostgreSQL and SQL Server.

## Verification

- Eight focused Framework scope-convention tests pass, including SQL Server
  design-model collation and index-friendly generated-query assertions.
- Task Runtime's 27 fast tests and both migration-drift checks pass after it
  adopts the shared convention without changing its provider models.
- Auth's 302 fast tests, both migration-drift checks, and exact PostgreSQL and
  SQL Server relational scenarios pass; the relational scenarios prove that
  `Tenant-Case` and `tenant-case` remain isolated.
- Notifications' 115 fast tests, both migration-drift checks, and focused SQL
  Server lifecycle scenario pass after its migration rebuilds affected indexes,
  keys, and foreign keys around the collation change.
- PostgreSQL models remain unchanged; SQL Server migrations own the required
  column-level schema changes.
