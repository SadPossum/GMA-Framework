# Atomic Distributed Rate Limiting

GMA provides a small, provider-neutral primitive for consuming several fixed-window quotas as one atomic decision. It is intended for security-sensitive ingress, expensive operations, and other workflows where independently updating credential, tenant, or global counters could partially consume capacity.

## Ownership Boundary

`Gma.Framework.RateLimiting` owns:

- bounded fixed-window partition and atomic-request value types;
- the `IMultiPartitionRateLimiter` provider contract;
- explicit `Acquired`, `Rejected`, and `ProviderUnavailable` outcomes;
- provider registration metadata and composition feature ids.

The framework does not own:

- tenant, credential, user, adapter, billing, or HTTP semantics;
- quota values or which partitions a product combines;
- fail-open or fail-closed business behavior;
- customer suspension, global kill switches, audit records, or operator APIs.

Products translate their own policy into bounded partition identities and decide how each outcome affects the use case.

## Providers

`Gma.Framework.RateLimiting.Infrastructure` supplies an in-memory provider for local development, tests, and single-process tools. It:

- atomically checks every partition before incrementing any partition;
- serializes overlapping partition sets with ordered lock stripes;
- aligns windows to Unix time;
- bounds opportunistic expired-entry cleanup;
- reports itself as non-distributed.

`Gma.Framework.RateLimiting.Redis` supplies the distributed provider. It:

- uses one Redis Lua script to check and increment every partition atomically;
- uses Redis server time so application-node clock skew cannot create quota drift;
- gives all keys in an atomic request the same Redis Cluster hash tag;
- hashes caller identities and partition descriptors with SHA-256 before storage;
- namespaces keys by application identity and host environment;
- returns `ProviderUnavailable` for Redis transport, timeout, or malformed-script-result failures;
- reports itself as distributed.

The Redis provider deliberately returns an unavailable decision instead of silently allowing work. The product still owns whether that decision denies, retries, queues, or degrades the operation.

## Contract Bounds

An atomic request contains:

- one stable atomic group;
- one positive permit count;
- between one and eight unique fixed-window partitions.

Partition identities and groups are length-bounded and cannot contain whitespace or control characters. Permit counts and limits are bounded positive integers. Windows use whole milliseconds between 100 milliseconds and 31 days.

Partition identity is scoped to its atomic group. Callers that need a shared
counter across requests must use the same atomic group and the same partition
descriptor. This matches Redis Cluster's requirement that every key touched by
one atomic script share a hash slot and keeps the in-memory and Redis providers
behaviorally equivalent.

Provider storage keys include the partition identity, limit, and window only through a hash. Changing a quota definition therefore starts a new physical counter instead of reinterpreting an existing counter under incompatible settings.

## Composition

Use exactly one provider:

```csharp
builder.AddInMemoryRateLimiting();
```

or:

```csharp
builder.Configuration["ConnectionStrings:redis"] = "...";
builder.AddRedisRateLimiting();
```

Registering two providers fails during composition. Consumers that require multi-node enforcement can inspect `IRateLimitProviderRegistration.IsDistributed` or require the distributed-provider composition feature.

## Verification

Framework unit tests cover contract guards, rollover, atomic rejection, concurrency, provider conflicts, options validation, and storage-key privacy. A product that enables the Redis provider should also exercise its composed policy through a Docker-backed Redis integration test before release.
