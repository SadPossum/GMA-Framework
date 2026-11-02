# Task Terminal Failure And Attempt Context Task

Status: complete
Date: 2026-08-15

## Goal

Let a task handler distinguish a terminal, machine-readable failure from an
ordinary retryable exception, and let handlers know whether the current lease
is the final configured attempt. Keep both capabilities generic and independent
from product task names, payloads, and domain failure policy.

## Problem

- The worker currently schedules every handler exception for retry, even when
  the handler has proved that another attempt cannot succeed.
- `TaskRun` persists `MaxAttempts`, but the immutable lease and
  `TaskExecutionContext` expose only the current attempt.
- Product handlers therefore cannot defer a durable domain failure until the
  final automatic attempt without duplicating runtime configuration.
- Persisting arbitrary exception messages would risk sensitive-data leakage;
  terminal failures need the same bounded error discipline as existing worker
  failures.

## Contract

1. `TaskRunLease` and `TaskExecutionContext` expose positive `MaxAttempts` and
   `IsFinalAttempt`; `Attempt` must not exceed `MaxAttempts`.
2. Existing direct constructor callers remain source-compatible. When old code
   omits `MaxAttempts`, the current attempt is treated as the maximum because no
   stronger run contract is available.
3. Persisted `TaskRun` leases always carry the run's configured
   `MaxAttempts`.
4. `TaskRunTerminalFailureException` carries one normalized, bounded,
   machine-readable failure code. It never persists an arbitrary exception
   message.
5. The worker catches that exception after cooperative cancellation and before
   the ordinary handler-failure path, then marks the run failed with no retry
   timestamp.
6. Ordinary exceptions, timeouts, host shutdown, cancellation, heartbeat,
   lease fencing, and explicit operator retry retain their existing behavior.

## Ownership

- GMA Tasks owns lease attempt metadata, failure-code validation, and worker
  terminal mutation behavior.
- A task-owning module decides which of its failures are terminal and whether a
  retryable domain failure should be exposed only on `IsFinalAttempt`.
- Applications remain responsible for domain state, operator recovery, and
  failure-code vocabulary.

## Guardrails

- No BunkFy export, owner, privacy, or storage vocabulary enters GMA.
- Terminal failure codes are bounded ASCII identifiers; exception messages and
  payload values are not persisted.
- The exception does not bypass lease ownership, context cleanup, metrics, or
  explicit admin retry.
- This change requires no Task Runtime schema or provider migration.

## Delivery

1. Extend immutable task lease and execution contracts.
2. Add the terminal-failure exception and validation tests.
3. Add worker proof for terminal versus retry-scheduled failure.
4. Update task-runtime documentation.
5. Run focused framework tests, then publish GMA before updating consumers.

## Completion Criteria

- handlers receive exact configured attempt bounds from persisted runs;
- terminal failures are persisted once with no automatic retry;
- ordinary failures still receive the configured backoff;
- unsafe terminal text cannot enter persisted task errors; and
- framework and consumer task tests pass.

## Verification

- focused Tasks contracts, runtime, and worker suite: 124 passed, 0 failed;
- package solution synchronization check: passed;
- package build: passed with 0 warnings and 0 errors; and
- complete non-Docker GMA Framework suite: 1,134 passed, 0 failed; and
- first adopter: the BunkFy Data Rights suite passed 523 tests, including
  terminal owner failures, transient non-final retries, final-attempt failure,
  and explicit operator recovery.
