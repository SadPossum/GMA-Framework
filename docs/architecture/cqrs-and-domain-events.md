# CQRS and Domain Events

The repo uses lightweight CQRS primitives from `Gma.Framework.Cqrs` instead of MediatR or another external dispatcher.

## Primitives

- `ICommand<TResponse>`
- `ITransactionalCommand<TResponse>`
- `IQuery<TResponse>`
- `ICommandHandler<TCommand, TResponse>`
- `IQueryHandler<TQuery, TResponse>`
- `IRequestDispatcher`
- `ICommandPipelineBehavior<TCommand, TResponse>`
- `ICommandOutcomeObserver<TCommand, TResponse>`
- `IQueryPipelineBehavior<TQuery, TResponse>`
- `ICommandValidator<TCommand>`
- `IQueryValidator<TQuery>`
- `Unit`
- `IUnitOfWork`
- `ITransactionalUnitOfWork`
- `IRollbackResettableUnitOfWork`

## Command Flow

```text
Endpoint
  -> command
  -> IRequestDispatcher
  -> validation behavior
  -> logging behavior
  -> outcome-observation behavior
  -> unit-of-work behavior, for transactional commands
  -> command handler
  -> domain changes
  -> owning module unit of work commit
```

Commands that write persistent module state implement `ITransactionalCommand<TResponse>`.
The unit-of-work behavior derives the owning module from the command assembly name and commits exactly one matching `IUnitOfWork`.
Commands that do not write persistent state may stay as plain `ICommand<TResponse>` and skip the UoW behavior.

Transactional commands roll back when a handler returns a failed result or
throws. Providers that retain mutable state after rollback implement the
optional `IRollbackResettableUnitOfWork` contract. The behavior resets that
state after each rollback it performs, using a non-request cancellation token, so a later
command in the same dependency-injection scope cannot inherit abandoned
mutations. EF-backed unit-of-work implementations provide this contract by
clearing the `DbContext` change tracker. If rollback and reset also fail while
handling an exception, the original operation exception remains authoritative
and carries the secondary cleanup failure in its diagnostic data.

## Cancellation Acceptance

The dispatcher treats caller cancellation as an execution boundary, not only as
a token that components may choose to observe. It checks cancellation before
invoking every command/query handler or pipeline behavior and after each
component returns normally. A cancellation-ignoring component therefore cannot
have its normally returned result accepted after the caller has canceled.

Transactional commands also check cancellation after transaction begin, after
the inner pipeline returns, and after save before commit. Cancellation observed
before a remaining persistence boundary enters the existing rollback and reset
path. Cleanup uses a non-request token so request cancellation cannot strand the
unit of work in an abandoned state.

These checks do not forcibly interrupt a component that is still running and
cannot undo a transaction that has already committed. Handlers and behaviors
must still cooperate with cancellation, and durable commands need
product-appropriate idempotency and unknown-outcome recovery. Do not implement
persistent mutation in a non-transactional command to bypass this boundary.

This is a small documented convention, not host composition scanning. Architecture tests guard module commands so state-writing commands stay explicit about transactionality.

## Command Outcome Observation

`ICommandOutcomeObserver<TCommand, TResponse>` is an optional best-effort hook
for facts that must be derived from a settled command result. It runs after the
inner unit-of-work behavior has committed or rolled back. Observer exceptions
are logged and cannot change the command result. The full observation phase
uses the bounded `Cqrs:OutcomeObservation:Timeout` budget independently of
request cancellation.

This hook is not a durable event bus and must not replace an owning module's
outbox. Validation failures that short-circuit before the observation behavior
are not observed. Use it for bounded operational evidence where the command
result is authoritative; use domain events and the transactional outbox for
business facts that must be delivered.

## Query Flow

Queries use the same dispatcher shape, but they should not commit unit-of-work changes. Keep query handlers side-effect free.
The shared query pipeline validates, logs, and records metrics, but it does not cache or open transactions automatically.
Explicit cache-aside stays inside query handlers so each module controls keys, tags, and failure-result policy.
Architecture tests guard query handlers from depending on UoW, outbox, or invalidation contracts.

## Validation

Validation belongs in `ICommandValidator<TCommand>` and `IQueryValidator<TQuery>` implementations.

Validators should:

- check request shape and application preconditions;
- return expected validation failures;
- avoid database writes;
- avoid business behavior that belongs in aggregates.

The default skeleton does not use FluentValidation. ADR 0007 keeps validation behind these shared CQRS contracts so public API, Admin API, CLI, tests, and generated modules all use the same result shape. A future module may add a FluentValidation adapter only when it has a concrete need for the richer rule model, and that adapter should still feed the shared CQRS validation pipeline.

## Domain Events

Aggregate roots collect domain events during behavior execution.
Concrete module domain events inherit `DomainEvent` for event id and occurrence time. Tenant-scoped domain events inherit `ScopedDomainEvent`, which also normalizes tenant id through the shared tenant rules.
Payload-specific fields remain in the owning module domain project so events still speak the module's ubiquitous language.

The module unit of work:

1. Finds changed aggregate roots.
2. Collects domain events.
3. Dispatches domain event handlers before the EF Core commit.
4. Saves changes once.
5. Clears domain events only after successful commit.

This allows a domain event handler to write outbox records in the same database transaction as the aggregate change.
EF-backed modules with domain events should inherit `EfDomainEventUnitOfWork<TDbContext>` from `Gma.Framework.Persistence.EntityFrameworkCore` and pass their module schema/name constant into the base constructor. Module-specific unit-of-work classes should stay thin; the shared base owns the dispatch/save/clear ordering. Their inbox stores should use `EfDomainEventInboxStore<TDbContext>` for the same reason: integration-event handlers are another application entry point, and aggregate domain events must not be skipped merely because the mutation arrived from the broker.

## Domain Event Handlers

Domain event handlers live in the owning module application layer.

Example:

```text
Gma.Modules.Auth.Domain.Events.MemberRegisteredDomainEvent
  -> Gma.Modules.Auth.Application.Handlers.MemberRegisteredOutboxProjector
  -> Gma.Modules.Auth.Persistence.AuthOutboxWriter
```

## Guidelines

- Commands mutate state.
- Persistent commands use `ITransactionalCommand<TResponse>`.
- Queries read state.
- Query handlers should not mutate persistent state or enqueue integration events.
- Aggregate methods enforce business invariants.
- Domain events describe something that already happened inside a module.
- Inherit domain events from `DomainEvent` or `ScopedDomainEvent` instead of re-declaring common metadata in every event type.
- Integration events describe something published across module or process boundaries.
- Do not publish integration events directly from command handlers.
- Keep command outcome observers payload-minimal and idempotent; they are
  best-effort observations, not transaction participants.
- Use domain event handlers to project committed domain facts into the outbox.
