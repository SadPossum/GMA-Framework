# Task Schedule Streaming Task

Status: complete
Date: 2026-08-05

## Goal

Let code-defined recurring schedules scale with large dynamic scope sets
without forcing every provider or scheduler tick to materialize all definitions
in memory. Keep scheduling generic, opt-in, and independent from product domain
types or a concrete scheduler product.

## Contract

- `ITaskScheduleProvider.GetSchedulesAsync(...)` returns a cancellation-aware
  `IAsyncEnumerable<ScheduledTaskDefinition>`.
- The hosted scheduler enqueues each yielded definition immediately and keeps a
  fixed observation time for the whole tick.
- Providers own discovery and schedule identity; the framework owns occurrence
  calculation and task-store enqueueing.
- Cursor memory is proportional to schedules present in the latest successful
  tick. Cursors for absent schedules are pruned only after complete enumeration.
- Reappearing schedules receive their declared `RunOnStart` behavior, while the
  persistent task-store dedupe key remains authoritative for duplicate work.

## Guardrails

- No tenant registry, EF type, BunkFy module, or product lifecycle policy enters
  the framework contract.
- Provider failure or cancellation cannot turn a partial enumeration into the
  authoritative active-schedule snapshot.
- Streaming does not weaken deterministic schedule names, payload versioning,
  scope identity, max-attempt policy, or per-occurrence deduplication.
- Compatibility list methods may remain behind application repository ports;
  production adapters can override those ports with direct EF async streams.

## Delivery

1. [x] Replace the materialized framework provider contract with async streaming.
2. [x] Enqueue definitions incrementally with cancellation propagation.
3. [x] Prune vanished cursors after successful ticks only.
4. [x] Cover incremental enqueueing and remove/re-add behavior in framework tests.
5. [x] Adapt BunkFy Reservations, Ingestion, Retention, and Data Rights providers.

## Verification

- Six focused framework scheduler tests cover incremental enqueueing, cursor
  pruning, incomplete-snapshot safety, retry behavior, and normal scheduling.
- Focused BunkFy provider and EF-stream tests pass for Reservations, Ingestion,
  Retention, and Data Rights.
- The BunkFy integration test assembly builds with zero warnings and errors.
- The complete BunkFy `eng/verify.ps1 -SkipRestore` gate passes solution and
  source-package checks, a zero-warning build, all migration drift checks, and
  all 4,310 non-Docker tests.
