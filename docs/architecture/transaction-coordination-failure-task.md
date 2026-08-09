# Transaction Coordination Failure Contract Task

Status: completed
Date: 2026-08-09
Completed: 2026-08-09

## Goal

Give every module that uses the shared EF transaction-key lock one stable,
provider-neutral failure contract when temporary database coordination cannot be
obtained, without teaching the Framework about organization, tenant, inventory,
reservation, or other product-owned lock keys.

## Audit Finding

`EfTransactionKeyLock` already provides bounded, transaction-scoped shared and
exclusive locking for PostgreSQL and SQL Server. Its successful behavior is
sound, but failure behavior is provider-specific:

- PostgreSQL command timeouts escape as Npgsql exceptions and configured
  database lock timeouts can escape as PostgreSQL errors;
- SQL Server returns distinct timeout, cancellation, and deadlock result codes,
  but the helper currently converts every negative result into a generic
  `InvalidOperationException`;
- production HTTP therefore reports expected temporary contention as an
  unexpected `500`, with no stable signal that the complete operation is safe
  to retry;
- cancellation requested by the caller must remain cancellation and must not be
  relabeled as contention.

SQL Server documents `sp_getapplock` result `-1` as timeout, `-2` as canceled,
`-3` as deadlock victim, and `-999` as a call or parameter error. PostgreSQL and
Npgsql likewise distinguish lock/deadlock errors, command timeout, and caller
cancellation. The Framework should normalize only the transient coordination
outcomes; configuration, unsupported-provider, and other database failures
must continue through the sanitized unexpected-failure path.

## Ownership

- `Gma.Framework.Cqrs` owns the generic transaction-coordination exception
  because it describes a failed transactional command boundary, independent of
  EF or an HTTP host.
- `Gma.Framework.Persistence.EntityFrameworkCore` translates provider outcomes
  from the shared key-lock adapter into that exception.
- `Gma.Framework.Api.Production` maps the generic retryable exception to a
  stable, sanitized HTTP response.
- Modules own their lock-resource namespaces, lock ordering, and any explicit
  timeout chosen for a workflow.
- Products own user-facing retry UX and must retry the complete command, never
  only the failed lock statement or a partially executed transaction.

## Contract

1. A transaction-key timeout, provider-side cancellation, or deadlock-victim
   outcome throws `TransactionCoordinationException` with a bounded semantic
   reason.
2. Caller-requested cancellation continues to throw
   `OperationCanceledException` and is never converted to a retryable server
   response.
3. Missing transactions, unsupported providers, invalid inputs, SQL Server
   call/parameter failures, and unclassified provider errors remain explicit
   non-transient failures.
4. The public exception message and HTTP response never contain the logical
   lock resource, its hash, provider return codes, SQL text, or application
   identifiers. The inner exception remains available to trusted diagnostics.
5. Production HTTP returns `503 Service Unavailable`, a short `Retry-After`
   header, a stable error title, and the request trace identifier.
6. The Framework does not retry automatically. The current unit of work rolls
   back first, and a caller may retry the entire idempotent command according to
   its own policy.
7. Existing timeout overloads and the default timeout remain source-compatible;
   this slice changes failure classification, not lock ordering or wait policy.

## Delivery

- [x] Add the generic exception and semantic failure reasons.
- [x] Normalize PostgreSQL and SQL Server timeout, cancellation, and deadlock
  outcomes while preserving caller cancellation.
- [x] Add the production HTTP exception handler and sanitized retry response.
- [x] Add focused unit coverage for provider-result classification, HTTP
  behavior, and cancellation precedence.
- [x] Add Docker-backed PostgreSQL and SQL Server proofs in the owning Framework
  suite for contention timeout, caller cancellation, rollback, and reacquisition.
- [x] Keep the normal Framework validation container-free and run provider
  proofs through the explicit Docker gate.
- [x] Run one completed-slice Framework gate and one focused Docker gate, then
  verify and publish Organizations, GMA Skeleton, and BunkFy consumer pins.

## Not In This Slice

- changing module lock keys or lock-order matrices;
- choosing a shorter Organizations-specific timeout without workload evidence;
- automatic command retries or endpoint-specific idempotency;
- distributed locks outside the current database transaction;
- exposing provider diagnostics in public responses.

## Completion Evidence

- Framework implementation checkpoint `6508523` passes a zero-warning solution
  build, 1,116 non-Docker tests, solution synchronization, repository security
  and release guards, and the transitive vulnerability audit.
- The focused provider gate proves contention timeout, caller cancellation,
  rollback, and reacquisition against PostgreSQL 16 and SQL Server 2022. It
  caught and fixed SqlClient's cancellation-specific `SqlException` behavior
  before publication.
- Organizations source remains unchanged at `732b0d8` and passes its boundary
  guard, zero-warning build, both-provider migration drift, 211 tests, and
  vulnerability audit against Framework `6508523`.
- GMA Skeleton checkpoint `2ae2a17` passes source-package and generated-selection
  checks, a zero-warning build, all migration drift checks, 1,116 Framework
  tests, 269 architecture tests, 17 integration tests, and all other fast
  reusable-module and example suites.
- BunkFy backend checkpoint `b5778e5` passes source synchronization, a
  zero-warning build, all GMA and product migration drift checks, every fast
  module and extension suite, 94 architecture tests, and 54 host integration
  tests. Public endpoints, OpenAPI, generated TypeScript, and web behavior are
  unchanged.
- BunkFy root checkpoint `b539efa` records the backend pin, and its latest-head
  guard confirms the backend, web, Framework, Extensions, and every reusable
  module exactly match their configured `dev` branches.
