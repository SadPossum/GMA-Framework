# Inbox Transaction Isolation Task

Status: complete

Date: 2026-08-10

## Goal

Make the generic EF inbox transaction contract explicit and efficient under concurrent delivery without weakening duplicate protection, atomic handler effects, or module-owned lifecycle admission.

## Evidence

A production-shaped downstream rehearsal delivered independent events for the same scope concurrently. PostgreSQL reported normal `40001` serialization failures from the generic inbox transaction, then NATS redelivery eventually recovered them. The work completed correctly, but the contention produced avoidable retries and noise.

The shared inbox had selected `Serializable` even though the durable guarantees come from narrower mechanisms: the inbox key, the atomic handler transaction, and each owner module's admission fence. Serializable isolation also prevents module-specific optimistic-concurrency retries from receiving their expected EF concurrency signal.

## Ownership

This is reusable messaging infrastructure and belongs in GMA Framework. Product event types, product lifecycle rules, and product-specific retry policy remain outside the framework.

## Contract

- Processing and failure-recording transactions use `ReadCommitted`.
- The unique `(event id, handler)` inbox key remains the duplicate-delivery guard, and the inbox row is persisted before the handler runs.
- Handler effects and the processed inbox marker commit in the same module database transaction.
- `IsAdmittedAsync(...)` remains inside that transaction. Modules that close, freeze, retire, or destroy a scope must coordinate the decision through their own transaction-scoped lock or concurrency token.
- A handler remains idempotent because broker redelivery and process failure can replay the complete operation.
- The framework does not retry only part of a handler transaction or infer whether external side effects are safe to repeat.
- NATS negative acknowledgement and redelivery remain the fallback for transient transaction failures that escape module-level concurrency handling.

## Verification

- Unit guard proves the shared inbox selects `ReadCommitted` for both transaction paths.
- Skeleton PostgreSQL coverage observes the real handler transaction isolation and retains rollback/retry behavior.
- Framework and Skeleton architecture, solution-sync, and focused messaging tests pass.
- A downstream candidate repeats the same concurrent rehearsal without unexplained PostgreSQL `40001` inbox failures.

## Done Criteria

- Framework behavior and documentation are published together.
- Skeleton integration coverage consumes the published Framework revision.
- Downstream composition updates only the Framework pointer; no product concept enters GMA.
- Production-shaped evidence confirms the contention regression is removed while the workflow still completes.

## Completion Evidence

- Framework solution sync and the complete non-Docker suite passed with 1,117 tests.
- Skeleton's existing PostgreSQL rollback/retry test observed `ReadCommitted` inside both failed and successful handler attempts, and its complete non-Docker suite passed.
- An exact downstream candidate repeated the same production-shaped concurrent rehearsal: its invitation, enrollment, and umbrella checks all passed and cleanup completed without failures.
- Fresh Worker logs from the candidate start through rehearsal completion contained no `40001`, serialization failure, warning, error, exception, or SQL-state matches. The corresponding API window was also clean.
