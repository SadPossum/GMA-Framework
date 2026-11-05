namespace Gma.Framework.Tests.RateLimiting;

using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Caching;
using Gma.Framework.Caching.Infrastructure;
using Gma.Framework.Caching.Redis;
using Gma.Framework.RateLimiting;
using Gma.Framework.RateLimiting.Redis;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tests.Support;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Xunit;

[Trait("Category", "Integration")]
[Trait("Category", "Docker")]
public sealed class RedisProviderContractTests
{
    private const int RedisPort = 6379;
    private const string RedisImage =
        "redis:8.2.11-alpine@sha256:489a769e0a54dc245896132a15c72d2bc7296719954da99398f43a3877e5f8eb";

    [DockerFact]
    public async Task Resp2_and_default_resp3_preserve_cache_isolation_atomic_limits_and_outage_contracts()
    {
        string password = Guid.NewGuid().ToString("N");
        await using IContainer redis = new ContainerBuilder(RedisImage)
            .WithPortBinding(RedisPort, assignRandomHostPort: true)
            .WithCreateParameterModifier(parameters =>
            {
                var bindings = parameters.HostConfig?.PortBindings;
                if (bindings is null || !bindings.TryGetValue("6379/tcp", out var ports) || ports is null || ports.Count == 0)
                {
                    throw new InvalidOperationException("The owned Redis fixture requires an explicit loopback port binding.");
                }
                foreach (var binding in ports)
                {
                    binding.HostIP = "127.0.0.1";
                }
            })
            .WithCommand("redis-server", "--requirepass", password, "--appendonly", "no",
                "--save", "", "--maxmemory", "64mb", "--maxmemory-policy", "noeviction")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
            .Build();
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(60));
        await redis.StartAsync(deadline.Token);
        string connection = $"{redis.Hostname}:{redis.GetMappedPublicPort(RedisPort)},password={password}";

        await VerifyAsync(connection, () => redis.StopAsync(), deadline.Token);
    }

    // The same real-provider contract can also be invoked by an owned local
    // executable rehearsal, without accessing the operator's Docker daemon.
    internal static async Task VerifyAsync(
        string connection,
        Func<Task> stopOwnedServer,
        CancellationToken cancellationToken)
    {
        List<ProviderCase> cases = [];
        using ConnectionMultiplexer observer = await ConnectionMultiplexer.ConnectAsync(
            connection + ",protocol=resp2,allowAdmin=true,connectTimeout=1000,asyncTimeout=1000,connectRetry=0");
        try
        {
            foreach (int protocol in new[] { 3, 2 })
            {
                ProviderCase current = new(connection, protocol);
                cases.Add(current);
                foreach (ServiceProvider root in current.Roots)
                {
                    await root.GetRequiredService<IDistributedCache>()
                        .GetAsync("owned-cache-connection-warmup", cancellationToken);
                }
                // HybridCache releases callers before its L2 write completes.
                // Hold real writes briefly so the persistence barrier below is
                // exercised without relying on an immediate cross-node hit.
                await observer.GetDatabase().ExecuteAsync("CLIENT", "PAUSE", 250, "WRITE");
                await VerifyCacheAsync(current, cancellationToken);
                await VerifyRateLimitsAsync(current, observer.GetDatabase(), cancellationToken);
                await VerifyActualProtocolAsync(current, observer);
            }

            await stopOwnedServer();
            foreach (ProviderCase current in cases)
            {
                await VerifyOutageAsync(current, cancellationToken);
            }
        }
        finally
        {
            foreach (ProviderCase current in cases)
            {
                await current.DisposeAsync();
            }
        }
    }

    private static async Task VerifyCacheAsync(ProviderCase current, CancellationToken cancellationToken)
    {
        CacheEntryPolicy policy = new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), localCacheEnabled: false);
        CacheKey key = CacheKey.Scoped("redis-contract", "shared");
        Assert.Equal(41, await current.Cache[0].GetOrCreateAsync(key, _ => ValueTask.FromResult(41), policy,
            cancellationToken: cancellationToken));
        await WaitForStoredAsync(current, 0, key, cancellationToken);
        // Independent roots and disabled L1 ensure this exercises the real Redis
        // adapter, rather than returning a process-local cache hit or fallback.
        Assert.Equal(41, await current.Cache[1].GetOrCreateAsync<int>(key,
            _ => throw new InvalidOperationException("A real shared-cache hit called its source."), policy,
            cancellationToken: cancellationToken));
        Assert.Equal(73, await current.Cache[2].GetOrCreateAsync(key, _ => ValueTask.FromResult(73), policy,
            cancellationToken: cancellationToken));
        await WaitForStoredAsync(current, 2, key, cancellationToken);
        Assert.Equal(41, await current.Cache[0].GetOrCreateAsync(key, _ => ValueTask.FromResult(999), policy,
            cancellationToken: cancellationToken));

        await current.Scopes[1].ServiceProvider.GetRequiredService<ICacheStore>()
            .RemoveAsync(key, cancellationToken);
        Assert.Equal(42, await current.Cache[0].GetOrCreateAsync(key, _ => ValueTask.FromResult(42), policy,
            cancellationToken: cancellationToken));
        Assert.Equal(73, await current.Cache[2].GetOrCreateAsync(key, _ => ValueTask.FromResult(999), policy,
            cancellationToken: cancellationToken));

        CacheKey expiring = CacheKey.Scoped("redis-contract", "expiring");
        CacheEntryPolicy shortPolicy = new(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100), localCacheEnabled: false);
        Assert.Equal(1, await current.Cache[0].GetOrCreateAsync(expiring, _ => ValueTask.FromResult(1), shortPolicy,
            cancellationToken: cancellationToken));
        await WaitForStoredAsync(current, 0, expiring, cancellationToken);
        Assert.Equal(1, await current.Cache[1].GetOrCreateAsync(expiring, _ => ValueTask.FromResult(99), shortPolicy,
            cancellationToken: cancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(2200), cancellationToken);
        Assert.Equal(2, await current.Cache[1].GetOrCreateAsync(expiring, _ => ValueTask.FromResult(2), shortPolicy,
            cancellationToken: cancellationToken));
    }

    private static async Task WaitForStoredAsync(
        ProviderCase current, int writer, CacheKey key, CancellationToken cancellationToken)
    {
        IDistributedCache adapter = current.Roots[writer].GetRequiredService<IDistributedCache>();
        string physicalKey = current.Scopes[writer].ServiceProvider.GetRequiredService<CacheKeyFormatter>().Format(key);
        long started = Stopwatch.GetTimestamp();
        while (await adapter.GetAsync(physicalKey, cancellationToken) is not { Length: > 0 })
        {
            Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5),
                "The actual Redis adapter did not persist its background cache write.");
            await Task.Delay(20, cancellationToken);
        }
    }

    private static async Task VerifyRateLimitsAsync(
        ProviderCase current, IDatabase observer, CancellationToken cancellationToken)
    {
        TimeSpan window = TimeSpan.FromHours(1);
        RedisResult[] serverTime = (RedisResult[])(await observer.ExecuteAsync("TIME"))!;
        long now = ((long)serverTime[0] * 1000) + ((long)serverTime[1] / 1000);
        long remaining = 3_600_000 - (now % 3_600_000);
        if (remaining < 10_000)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(remaining + 100), cancellationToken);
        }

        MultiPartitionRateLimitRequest concurrent = new("concurrent", 1,
            [new("principal", 8, window), new("network", 16, window)]);
        Task<MultiPartitionRateLimitDecision>[] attempts = Enumerable.Range(0, 64)
            .Select(index => current.Limiters[index % 2].AcquireAsync(concurrent, cancellationToken).AsTask())
            .ToArray();
        MultiPartitionRateLimitDecision[] decisions = await Task.WhenAll(attempts);
        Assert.Equal(8, decisions.Count(item => item.Outcome == MultiPartitionRateLimitOutcome.Acquired));
        Assert.Equal(56, decisions.Count(item => item.Outcome == MultiPartitionRateLimitOutcome.Rejected));
        Assert.All(decisions.Where(item => item.Outcome == MultiPartitionRateLimitOutcome.Rejected),
            item => Assert.InRange(item.RetryAfter!.Value, TimeSpan.FromTicks(1), window));

        FixedWindowRateLimitPartition narrow = new("narrow", 1, window);
        FixedWindowRateLimitPartition wide = new("wide", 2, window);
        Assert.Equal(MultiPartitionRateLimitOutcome.Acquired,
            (await current.Limiters[0].AcquireAsync(new("atomic", 1, [narrow]), cancellationToken)).Outcome);
        Assert.Equal(MultiPartitionRateLimitOutcome.Rejected,
            (await current.Limiters[1].AcquireAsync(new("atomic", 1, [narrow, wide]), cancellationToken)).Outcome);
        Assert.Equal(MultiPartitionRateLimitOutcome.Acquired,
            (await current.Limiters[0].AcquireAsync(new("atomic", 2, [wide]), cancellationToken)).Outcome);
        // Denial must consume no permits, and other atomic groups stay isolated.
        Assert.Equal(MultiPartitionRateLimitOutcome.Acquired,
            (await current.Limiters[1].AcquireAsync(new("other-group", 1, [narrow]), cancellationToken)).Outcome);

        FixedWindowRateLimitPartition expiring = new("expiring", 1, TimeSpan.FromSeconds(2));
        MultiPartitionRateLimitRequest expiryRequest = new("expiry", 1, [expiring]);
        Assert.Equal(MultiPartitionRateLimitOutcome.Acquired,
            (await current.Limiters[0].AcquireAsync(expiryRequest, cancellationToken)).Outcome);
        RedisKey physical = current.Roots[0].GetRequiredService<RedisRateLimitStorageKeyFormatter>()
            .Format(expiryRequest.AtomicGroup, expiring);
        TimeSpan? ttl = await observer.KeyTimeToLiveAsync(physical);
        if (ttl is not null)
        {
            Assert.InRange(ttl.Value, TimeSpan.FromTicks(1), expiring.Window);
            await Task.Delay(ttl.Value + TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        Assert.False(await observer.KeyExistsAsync(physical));
        Assert.Equal(MultiPartitionRateLimitOutcome.Acquired,
            (await current.Limiters[1].AcquireAsync(expiryRequest, cancellationToken)).Outcome);
    }

    private static async Task VerifyActualProtocolAsync(ProviderCase current, ConnectionMultiplexer observer)
    {
        IServer server = observer.GetServer(observer.GetEndPoints().Single());
        string clients = (string)(await server.ExecuteAsync("CLIENT", "LIST"))!;
        string[][] matching = clients.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Any(field => field.StartsWith("name=" + current.ClientPrefix, StringComparison.Ordinal)))
            .ToArray();
        Assert.True(matching.Length >= 5, "Real cache and limiter connections were not all observed.");
        Assert.All(matching, fields => Assert.Contains("resp=" + current.Protocol, fields));
    }

    private static async Task VerifyOutageAsync(ProviderCase current, CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        MultiPartitionRateLimitRequest request = new("offline", 1,
            [new("principal", 1, TimeSpan.FromMinutes(1))]);
        Assert.Equal(MultiPartitionRateLimitOutcome.ProviderUnavailable,
            (await current.Limiters[0].AcquireAsync(request, cancellationToken)).Outcome);
        int calls = 0;
        CacheEntryPolicy policy = new(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100), localCacheEnabled: false);
        Assert.Equal(1, await current.Cache[0].GetOrCreateAsync(CacheKey.Scoped("redis-contract", "offline"),
            _ => ValueTask.FromResult(++calls), policy, cancellationToken: cancellationToken));
        Assert.Equal(1, calls);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(10), "Redis outage exceeded its bounded provider timeout.");

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            current.Limiters[0].AcquireAsync(request, cancelled.Token).AsTask());
    }

    private sealed class ProviderCase : IAsyncDisposable
    {
        public ProviderCase(string connection, int protocol)
        {
            this.Protocol = protocol;
            string owner = Guid.NewGuid().ToString("N");
            this.ClientPrefix = owner + "-";
            this.Roots = Enumerable.Range(0, 3).Select(index => BuildProvider(
                connection + ",name=" + this.ClientPrefix + index +
                ",connectTimeout=1000,asyncTimeout=1000,syncTimeout=1000,connectRetry=0" +
                (protocol == 2 ? ",protocol=resp2" : ""),
                owner, index == 2 ? "beta" : "alpha")).ToArray();
            this.Scopes = this.Roots.Select(root => root.CreateScope()).ToArray();
            this.Cache = this.Scopes.Select(scope => scope.ServiceProvider.GetRequiredService<IApplicationCache>()).ToArray();
            this.Limiters = this.Roots.Take(2).Select(root => root.GetRequiredService<IMultiPartitionRateLimiter>()).ToArray();
        }

        public int Protocol { get; }
        public string ClientPrefix { get; }
        public ServiceProvider[] Roots { get; }
        public IServiceScope[] Scopes { get; }
        public IApplicationCache[] Cache { get; }
        public IMultiPartitionRateLimiter[] Limiters { get; }

        public async ValueTask DisposeAsync()
        {
            foreach (IServiceScope scope in this.Scopes)
            {
                scope.Dispose();
            }
            foreach (ServiceProvider root in this.Roots)
            {
                await root.DisposeAsync();
            }
        }

        private static ServiceProvider BuildProvider(string connection, string owner, string tenant)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                DisableDefaults = true,
                EnvironmentName = "RedisContract"
            });
            builder.Configuration["ConnectionStrings:redis"] = connection;
            builder.Configuration["Caching:Enabled"] = "true";
            builder.Configuration["Caching:Provider"] = "Redis";
            builder.Configuration["Caching:KeyPrefix"] = owner;
            builder.Configuration["Caching:Redis:InstanceName"] = owner;
            builder.Configuration["RateLimiting:Redis:InstanceName"] = owner;
            builder.Configuration["Tenancy:Enabled"] = "false";
            builder.AddTenancyInfrastructure();
            builder.AddTenantCaching();
            builder.AddCachingInfrastructure();
            builder.AddRedisCaching();
            builder.AddRedisRateLimiting();
            builder.Services.Replace(ServiceDescriptor.Singleton<ITenantContext>(new FixedTenant(tenant)));
            return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
    }

    private sealed record FixedTenant(string TenantId) : ITenantContext
    {
        public bool IsEnabled => true;
    }
}
