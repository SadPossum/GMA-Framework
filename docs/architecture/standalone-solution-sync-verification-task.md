# Standalone Solution Sync Verification Task

Status: complete
Date: 2026-07-28

## Goal

Make deterministic `.slnx` synchronization an enforced property of every
independent GMA source repository, rather than a manual release-time check.
Adding, removing, or moving an owned project, document, script, request, or
workflow must fail that repository's normal validation until its solution is
regenerated.

## Ownership

- GMA Framework owns `eng/sync-solution.ps1`, including deterministic
  discovery, folder classification, generation, and `-Check` comparison.
- Each independent Framework, Extensions, or module repository owns the exact
  solution filename passed to the framework tool in its validation workflow.
- GMA Skeleton owns the composition-level guard that discovers mounted source
  repositories and checks each package-local solution.
- No product repository, product name, or product-specific layout belongs in
  the shared tool or source-repository workflows.

## Design

Every independent source validation workflow checks solution synchronization
before restore. Framework invokes its checked-in tool directly. Extensions and
module workflows invoke the same tool from the Framework checkout already used
for source-first builds.

Skeleton adds one fast mounted-source guard:

1. require the Framework and Extensions mounts;
2. discover every directory immediately below `gma/modules`;
3. require exactly one root `.slnx` in each source repository;
4. invoke Framework `sync-solution.ps1 -Check` for every discovered solution;
5. fail before restore, build, migrations, tests, or Docker work.

The guard is part of normal Skeleton verification and focused GMA validation.
It does not rewrite files.

## Efficiency

- Solution checks are filesystem-only and run before expensive work.
- Local rollout verification uses the mounted-source guard, workflow syntax
  review, focused architecture tests, and repository diff checks.
- Docker is not run locally for workflow-only source-repository changes.
- Each repository is pushed once as a final candidate; GitHub Actions is not
  used as an edit loop.

## Delivery

1. [x] Add the Skeleton mounted-source solution guard and wire it into normal
   and focused validation.
2. [x] Guard the composition scripts and all reusable package discovery with
   focused architecture tests.
3. [x] Add the Framework-owned synchronization check to Framework CI.
4. [x] Add the same Framework-owned check to Extensions and all reusable module
   validation workflows.
5. [x] Prove all mounted solutions are synchronized.
6. [x] Publish each independent source repository once, then advance and
   validate the Skeleton pointers once.

## Acceptance

- every independent GMA source PR/push rejects a stale package-local solution;
- Skeleton validation rejects stale solutions in any mounted source package;
- newly mounted module repositories are discovered without a hard-coded module
  allowlist;
- the check runs before restore/build/test work;
- current Framework, Extensions, and all reusable module solutions pass;
- no duplicate solution-generation algorithm or product convention is added.
