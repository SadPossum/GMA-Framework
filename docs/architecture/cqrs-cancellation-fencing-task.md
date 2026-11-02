# CQRS Cancellation Fencing Task

Status: completed
Date: 2026-08-22
Completed: 2026-08-22

## Goal

Give every GMA command and query pipeline a stable cancellation-acceptance
boundary: a component must not be invoked after caller cancellation, and a
component that ignores cancellation must not have its normally returned result
accepted after cancellation is observed.

For transactional commands, stop before each remaining persistence boundary
when cancellation has already been observed and roll back any open transaction.

## Audit Finding

The dispatcher passes the caller token to handlers and pipeline behaviors, but
currently relies entirely on each component to observe it. A component can
ignore cancellation, return a successful result, and allow the pipeline to keep
running. In a transactional command, that normal result can reach
`SaveChangesAsync` or `CommitTransactionAsync` after the caller has canceled.

This is a generic CQRS execution concern shared by every GMA consumer. Domain
deadlines, replay proofs, idempotency keys, and owner-specific continuation
rules remain module or product concerns.

## Ownership

- `Gma.Framework.Cqrs.Infrastructure` owns invocation and result-acceptance
  fencing for command/query handlers and pipeline behaviors.
- The shared command unit-of-work behavior owns cancellation checkpoints before
  save and before transaction commit.
- Modules own cancellation-aware implementations, transactional command
  classification, idempotency, domain deadlines, and recovery semantics.
- Hosts own request-abort and shutdown token creation.

## Contract

1. A pre-canceled dispatch does not invoke a command/query handler or any
   command/query pipeline behavior.
2. The dispatcher checks caller cancellation after every handler or behavior
   returns normally and before accepting its result.
3. Existing null-task and null-result diagnostics remain unchanged for
   non-canceled calls.
4. A transactional command checks cancellation after beginning its transaction,
   after its inner pipeline returns, and after saving but before committing.
5. Cancellation before save or commit follows the existing rollback and reset
   path with cleanup protected from the canceled request token.
6. Failed results still roll back normally when the caller has not canceled.
7. Query handlers remain side-effect free; the fence is not a substitute for
   that architecture rule.

## Limits

- Cancellation is cooperative while a component is still running. This slice
  does not abandon an in-process task or dispose its scope while it may continue
  mutating state.
- Cancellation observed after a transaction has already committed cannot undo
  that commit. Durable commands still need product-appropriate idempotency and
  honest unknown-outcome recovery.
- Non-transactional commands that persist directly cannot be rolled back by the
  shared unit-of-work behavior and remain an architecture violation.
- Task Runtime hard-timeout enforcement is a separate runtime-isolation problem;
  this slice does not implement `WaitAsync`-style task abandonment.

## Delivery

- [x] Fence command/query handler and behavior invocation and normal-result
  acceptance in the dispatcher.
- [x] Add unit-of-work cancellation checkpoints around transaction, save, and
  commit boundaries.
- [x] Prove pre-canceled dispatch does not invoke components.
- [x] Prove normally returning cancellation-ignoring handlers and behaviors do
  not yield accepted results.
- [x] Prove cancellation before save and before commit rolls back without
  crossing the remaining boundary.
- [x] Update CQRS documentation and run one completed-slice Framework gate.

## Not In This Slice

- domain-specific deadlines or contributor protocols;
- automatic retries or idempotency-key design;
- process isolation for uncooperative task handlers;
- HTTP response mapping changes;
- changes to public CQRS interfaces or module contracts.

## Completion Evidence

- Focused CQRS coverage passed with 45 tests, including six dispatcher
  cancellation contracts and three new transaction-boundary cancellation
  contracts.
- The standalone solution synchronization check passed after adding this task
  document to `Gma.Framework.slnx`.
- The serial Framework solution build passed with 0 warnings and 0 errors.
- The complete non-Docker Framework suite passed with 1,147 tests and no skips.
- No Docker/provider gate was run because this slice changes only the in-process
  CQRS dispatcher and in-memory-testable unit-of-work orchestration.
