# 0010 Projection Rebuild Tasks

## Status

Accepted.

## Context

Event consumers keep local projections current for future changes, but they cannot repair or backfill existing rows when a consumer is introduced late, a projection schema changes, an event handler had a bug, or an operational repair is needed.

The project already has an optional task runtime, explicit module metadata, provider-split EF persistence, tenant-aware DbContexts, and strict cross-module boundaries. Projection rebuilds should use those building blocks instead of adding an external scheduler or hidden cross-module data access.

## Decision

Projection rebuilds are explicit module task handlers backed by `Gma.Framework.ProjectionRebuild` plus the optional `Gma.Framework.ProjectionRebuild.Tasks` adapter when the caller is the task runtime.

- The consuming module owns the rebuild task, destination projection, writer, and checkpoint table.
- The producing module exposes source data through a public contract/export port, not through domain, application, or persistence internals.
- Checkpoints are persisted in the consuming module schema and keyed by run id, projection name, and scope id when scope-aware.
- The shared runner owns batching, checkpoint load/save, observer callbacks, and bounded metrics without referencing task runtime contracts.
- The task adapter maps rebuild observer callbacks to task progress reporting and cooperative task control polling.
- TaskRuntime owns enqueueing, leasing, retry, timeout, admin control, and tenant-context setup.
- Hosts compose rebuild workers explicitly by registering the consumer module and the task worker group.

The first compiled example is `Ordering` rebuilding its local Catalog item projection from `Catalog.Contracts/Exports`.

## Consequences

This keeps rebuilds operationally real while preserving module ownership:

- consumer modules can repair local state without foreign keys or producer EF references;
- source/export contracts become stable compatibility points;
- retrying the same task run resumes from the consumer-owned checkpoint;
- provider migrations cover checkpoint tables like any other module persistence;
- rebuilds remain optional because no default host registers example modules or projection worker groups.

The tradeoff is a little more boilerplate per rebuild. That is intentional: the data semantics, cursor, tombstone policy, and idempotent writer rules are module-specific and should not be inferred by reflection or EF naming conventions.

## Follow-Ups

- Add richer per-module structured logs if an operational dashboard needs them.
- Add full-rebuild and tombstone policies when a real projection needs deletion detection.
- Add high-water-mark catch-up when a projection must coordinate tightly with live event handlers.
