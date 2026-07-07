# Projection Rebuild Implementation Notes

These are temporary working notes for the projection rebuild slice. Keep useful findings here while implementing, then either fold them into durable docs or delete the temporary sections before the release squash.

## Current Direction

- Keep rebuild tasks explicit module capabilities on top of `Gma.Framework.Tasks`; do not add a second scheduler or hidden projection scanner.
- Add a narrow shared projection rebuild package for operational contracts and orchestration only.
- Keep task progress/control adaptation in `Gma.Framework.ProjectionRebuild.Tasks` so the core rebuild loop can be reused by admin endpoints, migration tools, or one-off repair hosts without pretending to be a task run.
- Let `TaskRuntime` own run lifecycle, retries, cancellation, worker leases, admin enqueue/list/get/cancel/retry, and progress display.
- Let the consuming module own projection repair state and checkpoint persistence because it owns the projection table.
- Keep checkpoint and transaction-boundary contracts in `Gma.Framework.ProjectionRebuild` and EF persistence helpers in `Gma.Framework.ProjectionRebuild.EntityFrameworkCore` so non-EF modules can use the rebuild loop without taking EF dependencies.
- The first concrete implementation is `Ordering` rebuilding its local catalog item projection from a `Catalog.Contracts` export source.

## Architecture Decisions

- `Ordering.Application` owns the rebuild task handler because it owns the projection and decides how snapshots are written.
- `Catalog.Contracts` owns the export snapshot and source contract. `Ordering.Application` may reference this contract, but it must not reference `Catalog.Application`, `Catalog.Persistence`, `Catalog.Domain`, or Catalog EF entities.
- `Catalog.Persistence` implements the Catalog export source because it can read Catalog-owned tables and return only the public export contract.
- `Ordering.Persistence` implements the Ordering projection writer and checkpoint store because both write Ordering-owned tables.
- Rebuild checkpoint stores are module-qualified and resolved through a registry, mirroring the outbox writer registry pattern.
- Checkpoints are keyed by task run id plus projection name and tenant id. Retrying the same task run resumes from the last persisted cursor.
- EF-backed checkpoint stores inherit `EfProjectionRebuildCheckpointStore<TDbContext,TState>` and checkpoint rows inherit `ProjectionRebuildCheckpointState`. This removes repeated save/load/mapping code while preserving module-owned schemas, migrations, and explicit DI registration.
- Ordering registers `OrderingProjectionRebuildTransactionBoundary` over `OrderingDbContext`, so each catalog projection write batch and checkpoint save share one EF transaction. Modules opt in only when writer and checkpoint effects share a transaction-capable store.
- Rebuild writes use idempotent upserts. A retry after a persisted checkpoint should continue from the next cursor, while a retry without a checkpoint should safely reprocess from the start.
- The first implementation uses keyset paging by normalized Catalog SKU. It avoids offset paging for large tables and avoids loading whole projections into memory.
- Live event handlers stay enabled. Both event handlers and rebuild writer use the same idempotent projection write path.

## Safety Rules To Preserve

- No cross-module database foreign keys.
- No consumer references to producer internals.
- No NATS requirement for rebuilds.
- No default host registration for Catalog or Ordering examples.
- Tenant-scoped rebuilds require tenant id from the task run context.
- Payloads must stay small and contain no secrets or connection strings.
- Metrics/log tags must stay bounded; tenant ids, cursors, and run ids belong in structured logs/progress/checkpoints, not metric tags.

## Planned Shared Contracts

- `ProjectionReadBatch<TSnapshot>`: bounded source page plus next cursor and completion flag.
- `ProjectionWriteResult`: written/skipped/failed counts for one batch.
- `ProjectionRebuildCheckpoint`: cursor, counters, projection version, updated timestamp, completion timestamp.
- `ProjectionRebuildRequest`: projection name, version, batch size, dry-run flag, optional cursor override.
- `IProjectionRebuildSource<TSnapshot>`: reads source pages.
- `IProjectionRebuildWriter<TSnapshot>`: writes or dry-runs a batch idempotently.
- `IProjectionRebuildCheckpointStore`: module-qualified checkpoint store.
- `IProjectionRebuildCheckpointStoreRegistry`: resolves checkpoint stores by module.
- `ProjectionRebuildRunner<TSnapshot>`: task-neutral loop, checkpoint load/save, observer callbacks, and metrics.
- `TaskProjectionRebuildRunner<TSnapshot>`: task adapter that maps rebuild observer callbacks to `ITaskRuntimeReporter` and `ITaskControlLoop`.

## Example Shape

- Catalog contracts:
  - `CatalogItemProjectionExport`
  - `ICatalogItemProjectionExportSource`
- Ordering contracts/application:
  - `RebuildCatalogItemProjectionPayload`
  - `RebuildCatalogItemProjectionTaskHandler`
  - payload-owned module task metadata `rebuild-catalog-item-projections`, worker group `projection-workers`, payload version `1`, control-enabled.
- Ordering persistence:
  - `OrderingProjectionRebuildCheckpoint`
  - `OrderingProjectionRebuildCheckpointStore`
  - `OrderingProjectionRebuildTransactionBoundary`
  - `CatalogItemProjectionRebuildWriter`
  - migration for `ordering.projection_rebuild_checkpoints`

## Side Findings

- `TaskRuntime` already has generic admin CLI/API enqueue, get, list, stats, control, cancel, and retry. This slice can use those surfaces rather than creating custom Ordering admin commands immediately.
- The current task progress message is useful but not enough for production resume because payloads are not mutated and handlers do not receive previous progress. A durable consumer-owned checkpoint table closes that gap.
- `CatalogItemProjectionRepository` already has idempotent single-row upsert. Prefer reusing that path from the rebuild writer so live event behavior and rebuild behavior stay aligned.
- Existing architecture tests already compare task metadata to handler registrations. Adding Ordering task metadata and registration should automatically get coverage there.
- Focused unit tests now cover shared runner behavior, checkpoint resume, cursor override, failed writer behavior, optional transaction-boundary wrapping, bounded metrics, the shared EF checkpoint adapter, Catalog export contracts, Ordering task registration, and Ordering checkpoint state.
- The dependency-boundary pass moved task progress/control adaptation out of core `Gma.Framework.ProjectionRebuild` and into `Gma.Framework.ProjectionRebuild.Tasks`.
- A Docker-backed integration test composes TaskRuntime, Catalog, and Ordering explicitly, applies migrations, seeds Catalog, runs the real worker, and verifies Ordering projections plus checkpoints across SQL Server and PostgreSQL.
- The separate projection rebuild store refactor scratch note was folded into this page and the durable architecture docs. Keep future findings here only while they are actively being worked.

## Future Audit Targets

- Consider a dedicated projection-rebuild admin front door only after multiple modules need nicer operator UX than the generic TaskRuntime enqueue command.
- Consider streaming source reads only if keyset batches become insufficient for very large exports.
- Consider high-water mark catch-up support if a future projection cannot tolerate event/rebuild race windows through idempotent upserts alone.
- Consider a tombstone/full-rebuild policy once a projection needs to remove destination rows that no longer exist in the source.
