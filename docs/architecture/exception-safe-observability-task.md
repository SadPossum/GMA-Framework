# Exception-Safe Observability Task

Status: implemented and locally verified; hosted evidence is product-owned

## Goal

Make GMA's generic operational failure signals safe for products that process
personal, confidential, or provider-controlled data. Framework and reusable
module infrastructure must log bounded operation metadata and exception type
names without attaching exception objects, messages, stack traces,
`ToString()` output, tenant/user/resource identifiers, or cache identities to
normal telemetry sinks.

This is a transport-level invariant. Products still decide which identifiers,
fields, error codes, traces, and notifications are permitted.

## Ownership

- GMA logging paths emit stable operation metadata and exception type names.
- Tenant and message enrichment records bounded scope-presence flags rather
  than concrete tenant, user, resource, message, or actor identifiers.
- GMA repository tests reject exception-overload logging and explicit
  exception message, stack-trace, or `ToString()` arguments.
- Generic request logging and handled HTTP failures retain route-template,
  method, status, duration, trace, module, and exception-type signals without
  exporting concrete URLs or exception details.
- Generic task, inbox, and outbox retry state stores stable failure codes and
  bounded exception types instead of arbitrary exception or contributor text.
- Product hosts decide exporter destinations, retention, access, redaction,
  and whether concrete URL/path data is permitted.
- Product modules remain responsible for not placing sensitive data in
  operation names, error codes, scope contributors, notification content, or
  other nominally bounded fields.

## Delivery

1. Replace raw exception logging in Framework infrastructure with an
   `ExceptionType` property.
2. Remove high-cardinality tenant, user, actor, message, notification, task-run,
   subscription, and cache identifiers from generic log events and scopes.
3. Add a source architecture guard covering non-generated Framework source.
4. Apply the same rule to reusable modules that still attach exception
   objects, beginning with TaskRuntime.
5. Preserve retry, fail-open/fail-closed, cancellation, and rethrow behavior;
   only telemetry content changes.
6. Verify representative logger captures contain the exception type but not an
   unmistakable exception-message canary.
7. Verify generic request diagnostics, handled HTTP failures, and persisted
   runtime failure state reject unmistakable personal-data canaries.

## Acceptance

- No Framework production source supplies an exception object to an
  `ILogger.Log*` call.
- No Framework production source supplies `.Message`, `.StackTrace`, or
  `.ToString()` derived from an exception to an `ILogger.Log*` call.
- Generic log templates and scopes contain no concrete tenant, scope, user,
  actor, message, notification, delivery, task-run, subscription, or cache
  identity.
- Failure logs retain enough bounded metadata to locate the affected command,
  query, task, message, notification, cache operation, or admin operation.
- Existing control flow, retries, results, and exception propagation remain
  unchanged.
- Framework validation and affected reusable-module validation pass.

## Verification

- Framework tests: 1,018 passed.
- Composed BunkFy non-Docker verification: passed with zero build warnings,
  synchronized solutions, clean package checks, and no migration drift.
- Composed Docker integration verification: 33 passed.

## Non-Goals

- Guessing whether arbitrary strings contain personal data.
- Logging request, command, event, task-payload, provider-payload, or response
  objects after redaction.
- Defining product field catalogues or legal retention policy in GMA.
- Replacing product-specific exporter and support-access controls.
