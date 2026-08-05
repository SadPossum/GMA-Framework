# Scoped Inbox Admission Task

Status: complete
Date: 2026-08-04

## Goal

Allow an EF-backed module inbox to suppress a late scoped message inside the
same serializable transaction that would otherwise create the inbox row and
invoke its handler. This supports module-owned lifecycle tombstones without
placing tenant policy, product workflow, or a central scope registry in the
messaging framework.

## Contract

- `EfInboxStore<TDbContext>` exposes one protected asynchronous admission hook.
- The default admits every message, preserving current behavior for all stores.
- A module may inspect or mutate its own state through the same scoped
  `DbContext` and transaction before inbox insertion.
- The protected context accessor and virtual processed-row cleanup are generic
  extension points for module-owned admission and lifecycle cleanup rules.
- Global messages remain a module decision; the framework does not infer scope
  semantics.
- A denied message returns an explicit payload-free `Suppressed` process result.
  NATS acknowledges `Suppressed` exactly like `Processed` and `Duplicate`.
- A duplicate processed message remains `Duplicate`; admission does not rewrite
  existing idempotency evidence.
- If a handler fails and admission becomes denied before failure evidence is
  recorded, the final result is `Suppressed` and no failed inbox row regrows.

## Guardrails

- No product, tenant-termination, retention, or module-specific type enters the
  framework API.
- Admission runs once per transaction and receives only the existing
  `InboxMessageRecord` plus cancellation.
- Exceptions and cancellation retain existing retry behavior; only an explicit
  `false` is acknowledged as suppressed.
- Metrics expose `suppressed` as a bounded status value.
- Focused tests cover default compatibility, pre-handler suppression, duplicate
  precedence, post-failure suppression, cancellation, and NATS acknowledgement.

## Delivery

1. [x] Add the protected hook and `Suppressed` result/status.
2. [x] Update NATS acknowledgement and bounded observability values.
3. [x] Add framework unit tests without requiring Docker.
4. [x] Consume the hook from one module-owned scope lifecycle.

The Notifications module is the first consumer. It suppresses messages for a
closed module-owned scope and preserves processed rows for that scope until its
destruction workflow removes the operational journal. Focused framework tests
cover the default-compatible path, suppression before handler execution,
duplicate precedence, suppression after handler failure, and NATS
acknowledgement.
