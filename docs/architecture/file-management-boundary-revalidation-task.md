# File Management Boundary Revalidation Task

Status: completed
Date: 2026-08-05
Completed: 2026-08-05

## Goal

Revalidate the shared file-management metadata contract and the optional Files front door at their production boundaries without adding product file lifecycle to Framework.

## Ownership

- Framework owns provider-neutral object metadata syntax and canonicalization.
- Files owns private-user HTTP limits, quarantine orchestration, compatibility reads/deletes, and download behavior.
- detector and inspector adapters inspect a borrowed read-only stream during the call; they never own or mutate quarantine bytes.
- hosts own concrete detector/scanner implementations, non-cookie authentication for the generic API front door, edge limits, rate limiting, quotas, and storage operations.
- product modules own business references, authorization, retention, legal hold, public delivery, and orphan reconciliation.

## Slice

1. Parse media types structurally in Framework, canonicalize valid parameterized values to their media-type essence, and reject malformed values, wildcards, and header injection.
2. Make Files quarantine views read-only and non-owning so an adapter cannot mutate or close the bytes consumed by later gates and storage.
3. Delete both current and legacy object representations so a migrated legacy copy cannot reappear after a successful delete.
4. Bound the complete multipart request in addition to each file section.
5. Force attachment handling even when a stored object has no usable filename.
6. Cover each boundary with focused regression tests and document the explicit non-cookie-authentication requirement for the antiforgery opt-out.

## Non-Goals

- a persistent file catalog, resumable uploads, public URLs, or product attachment workflows;
- choosing a MIME detector or malware scanner;
- changing storage-provider selection or object-key layout;
- making Framework aware of Files, ASP.NET Core, tenants, or product authorization.

## Acceptance

- shared content types are structurally valid, concrete, and canonical;
- detector and inspector adapters cannot write, truncate, or own the quarantine stream;
- deleting an id removes both current and legacy copies and a later read returns not found;
- one HTTP request cannot multiply the per-file allowance with extra multipart sections;
- all private downloads include `Content-Disposition: attachment`;
- Framework and Files focused tests, full fast suites, boundary checks, solution sync, and package audits pass.

## Evidence

- Framework solution synchronization and zero-warning build passed.
- All 1,109 Framework tests passed, including 16 focused file-management tests.
- Framework repository security and release checks passed; the transitive package audit reported no vulnerable packages.
- Files solution synchronization, zero-warning build, module-boundary check, repository security/release checks, and all 26 tests passed.
- The Files transitive package audit reported no vulnerable packages.
