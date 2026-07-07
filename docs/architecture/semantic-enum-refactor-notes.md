# Semantic Enum Refactor Notes

Date: 2026-07-05

## Decision

Closed domain vocabularies should be regular enums with `Unknown = 0` unless they need richer compatibility behavior than .NET enums can express. Keep wire/storage names in small owner-package helpers such as `NotificationSeverityNames`, not as string-backed domain value objects.

This follows the .NET guidance to use enums for small closed value sets, to provide a zero value, and to avoid custom enumeration classes unless the values need richer business behavior of their own.

This keeps the skeleton simple:

- domain state is strongly typed;
- API/CLI/event contracts use typed enums with explicit `Unknown = 0` values where they expose closed choices;
- database strings stay stable at persistence boundaries through owner-package parse/format helpers;
- invalid or unknown values fail before they become meaningful business state;
- no shared smart-enum base is introduced until at least two real features need one.

## Applied In This Slice

- Replaced notifications domain string-backed `NotificationSeverity`, `NotificationBroadcastAudience`, and `NotificationBroadcastRecipientKind` records with enums.
- Replaced notification recipient-kind public constants with a `Gma.Modules.Notifications.Contracts.NotificationBroadcastRecipientKind` enum and mapped contract values to domain values in `Gma.Modules.Notifications.Application`.
- Replaced notification DTO and create-broadcast command severity strings with `Gma.Modules.Notifications.Contracts.NotificationSeverity`.
- Added notification-owned parse/format helpers for stable wire values.
- Added owner-package JSON converters for notification contract enums and shared live notification severity so HTTP, SignalR/SSE payloads, and integration events write stable lowercase/kebab-case strings and reject numeric, unknown, or undefined values.
- Replaced the shared SSE notification item `kind` string with `NotificationSseItemKind`, keeping the external JSON as stable `notification`/`heartbeat` text.
- Replaced Auth admin member status DTO strings with `Gma.Modules.Auth.Contracts.MemberStatus` and an Auth-owned JSON converter so admin API responses expose stable lowercase status names without coupling contracts to the Auth domain enum.
- Added owner-package JSON converters and wire-name helpers for `Gma.Modules.Auth.Contracts.UsernameType`, `Catalog.Contracts.CatalogItemStatus`, and `Ordering.Contracts.OrderStatus`.
- Kept EF columns and public DTO strings stable by translating through helpers in persistence/repositories.
- Changed notification aggregate/entity factories to accept domain enum values instead of raw strings.
- Added tests for `Unknown = 0`, parsing, formatting, and invalid-value rejection.
- Replaced shared administration audit-result strings with `AdminAuditResult`, while keeping persisted audit rows as stable lowercase wire names through `AdminAuditResults`.
- Changed `InboxMessageStatus` to reserve `Unknown = 0`; provider-specific compatibility migrations remap existing inbox rows from the old `Pending = 0` layout before runtime code sees them.
- Hardened the notification history writer and architecture guards so undefined enum values are rejected or preserved as `Unknown`, never silently collapsed into valid states such as `Info` or `Active`.

## Audit Notes

The remaining string-name helpers found in shared packages are intentionally boundary/runtime concerns, not domain semantic state:

- `TaskRunStatusNames` already wraps the `TaskRunStatus` enum for task wire names.
- `TaskControlCommandNames` represents extensible control message names crossing scheduler/worker boundaries.
- `MetricTagValues` normalizes bounded observability tags and should not become domain types.

`AdminAuditEntry.Result` intentionally remains a string at the EF persistence edge so existing audit rows and migrations stay stable. Shared/admin code should use `AdminAuditResult` or `AdminAuditRecord.Result`, not compare result strings directly.

`JwtSettings.Audience` intentionally remains a configuration string; it is an OpenID/JWT audience value, not an application semantic enum. Architecture guard tests now scan source boundaries for semantic string fields such as `Status`, `Severity`, `Audience`, `Kind`, and `Provider`, with only these storage/configuration exceptions allowed.

Public JSON enum serialization must be package-owned when stable text matters. Do not rely on host-wide `System.Text.Json` enum settings for module contracts. Add an explicit owner-package converter and tests, as notifications now does for severity, broadcast audience, and broadcast recipient kind, and Auth now does for admin member status.

Switch defaults in enum mapping code must return `Unknown`, throw, or return a failure result. They must not map to a valid domain value as a fallback, because that hides producer or storage drift and makes later diagnosis painful.

## References

- [Enum design guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/enum)
- [CA1008: Enums should have zero value](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1008)
- [Customize System.Text.Json enum serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings)
- [Use enumeration classes instead of enum types](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/enumeration-classes-over-enum-types)
- [EF Core value conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
