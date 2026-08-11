# Task Handler Timeout Override Task

Status: complete
Date: 2026-08-11

## Goal

Allow an optional task-handler registration to declare its own execution
timeout while preserving the worker-wide `Tasks:Worker:HandlerTimeout` as the
fallback. Keep timeout policy in the task-owning module and keep the runtime
independent from product task names, payloads, and persistence schemas.

## Problem

- The worker currently applies one timeout to every registered task handler.
- Different task kinds can have materially different bounded execution needs.
- Raising the global timeout makes every unqualified handler slower to recover;
  keeping it low can cancel a deliberately bounded long-running handler before
  its own contract deadline.
- The handler registration is already the authoritative runtime mapping from a
  versioned task identity to its implementation and worker group.

## Contract

1. `TaskHandlerRegistration` exposes an optional positive `HandlerTimeout`.
2. Both `AddTaskHandler` registration forms accept the timeout as a trailing
   optional argument, preserving existing source behavior.
3. The worker uses the registration timeout when present and otherwise uses
   `TaskWorkerOptions.EffectiveHandlerTimeout` exactly as today.
4. Timeout cancellation, automatic heartbeat, retry scheduling, host shutdown,
   and terminal mutation semantics remain unchanged.
5. Duplicate registrations with different timeout metadata conflict instead of
   silently selecting one.
6. Timeout values are rejected when they cannot be represented safely by the
   runtime cancellation timer.

## Ownership

- GMA Tasks owns the generic registration metadata and runtime enforcement.
- A task-owning module decides whether its handler needs an explicit timeout.
- Hosts retain the global fallback for handlers that do not opt in.
- Task Runtime persistence remains unchanged because execution timeout belongs
  to compiled handler composition, not to an individual queued run.

## Guardrails

- No BunkFy module, worker group, owner coordinate, or retention rule enters
  GMA.
- No timeout is inferred from arbitrary payload JSON or schedule intervals.
- Existing registrations and persisted task runs remain compatible.
- Per-handler timeouts do not disable heartbeat or weaken lease-generation
  checks.

## Delivery

1. Extend task-handler registration and service registration APIs.
2. Resolve the effective timeout inside the worker after exact handler lookup.
3. Add contract tests for validation, idempotency, and conflicting metadata.
4. Add worker tests proving override and fallback behavior.
5. Update Tasks documentation and run focused framework verification.

## Completion Criteria

- existing handlers still use the configured worker fallback;
- an explicitly configured handler can safely run beyond that fallback;
- invalid or conflicting timeout metadata fails before execution;
- heartbeat, retry, and shutdown tests remain green; and
- no task persistence or module-descriptor contract changes.

## Verification

- focused Tasks contract and worker suite: 121 passed, 0 failed;
- GMA solution synchronization check: passed; and
- consolidated BunkFy repository verification: passed, including the complete
  GMA framework suite at 1,123 passed, 0 failed.
