namespace Gma.Framework.Tests;

using Gma.Framework.RateLimiting;
using Gma.Framework.RateLimiting.Infrastructure;
using Gma.Framework.RateLimiting.Redis;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RateLimitingTests
{
    private static readonly DateTimeOffset WindowStart =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Fixed_window_partitions_reject_unbounded_or_unstable_inputs()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new FixedWindowRateLimitPartition("tenant alpha", 1, TimeSpan.FromMinutes(1)));
        _ = Assert.Throws<ArgumentException>(() =>
            new FixedWindowRateLimitPartition(
                new string('a', FixedWindowRateLimitPartition.IdentityMaxLength + 1),
                1,
                TimeSpan.FromMinutes(1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FixedWindowRateLimitPartition("tenant:alpha", 0, TimeSpan.FromMinutes(1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FixedWindowRateLimitPartition("tenant:alpha", 1, TimeSpan.FromTicks(1)));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FixedWindowRateLimitPartition(
                "tenant:alpha",
                1,
                FixedWindowRateLimitPartition.MaximumWindow + TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public void Atomic_requests_validate_and_defensively_copy_partitions()
    {
        FixedWindowRateLimitPartition first =
            Partition("tenant:alpha", permitLimit: 10);
        var source = new List<FixedWindowRateLimitPartition> { first };

        var request = new MultiPartitionRateLimitRequest(
            "adapter-ingress:alpha",
            permitCount: 2,
            source);
        source.Clear();

        Assert.Single(request.Partitions);
        _ = Assert.Throws<ArgumentException>(() =>
            new MultiPartitionRateLimitRequest(
                "adapter-ingress:alpha",
                permitCount: 1,
                [first, first]));
        _ = Assert.Throws<ArgumentException>(() =>
            new MultiPartitionRateLimitRequest(
                "adapter-ingress:alpha",
                permitCount: 11,
                [first]));
        _ = Assert.Throws<ArgumentException>(() =>
            new MultiPartitionRateLimitRequest(
                "adapter ingress alpha",
                permitCount: 1,
                [first]));
    }

    [Fact]
    public async Task In_memory_provider_rejects_after_limit_and_resets_at_window_boundary()
    {
        MutableClock clock = new(WindowStart);
        var limiter = new InMemoryMultiPartitionRateLimiter(clock);
        var request = new MultiPartitionRateLimitRequest(
            "adapter-ingress:alpha",
            permitCount: 1,
            [Partition("tenant:alpha", permitLimit: 1)]);

        MultiPartitionRateLimitDecision first =
            await limiter.AcquireAsync(request);
        MultiPartitionRateLimitDecision second =
            await limiter.AcquireAsync(request);

        Assert.Equal(MultiPartitionRateLimitOutcome.Acquired, first.Outcome);
        Assert.Equal(MultiPartitionRateLimitOutcome.Rejected, second.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(1), second.RetryAfter);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        MultiPartitionRateLimitDecision afterRollover =
            await limiter.AcquireAsync(request);

        Assert.Equal(
            MultiPartitionRateLimitOutcome.Acquired,
            afterRollover.Outcome);
    }

    [Fact]
    public async Task Rejected_atomic_request_does_not_consume_any_partition()
    {
        var limiter = new InMemoryMultiPartitionRateLimiter(
            new MutableClock(WindowStart));
        FixedWindowRateLimitPartition constrained =
            Partition("credential:one", permitLimit: 1);
        FixedWindowRateLimitPartition shared =
            Partition("tenant:alpha", permitLimit: 10);
        var combined = new MultiPartitionRateLimitRequest(
            "adapter-ingress:alpha",
            permitCount: 1,
            [constrained, shared]);

        Assert.Equal(
            MultiPartitionRateLimitOutcome.Acquired,
            (await limiter.AcquireAsync(combined)).Outcome);
        Assert.Equal(
            MultiPartitionRateLimitOutcome.Rejected,
            (await limiter.AcquireAsync(combined)).Outcome);

        var consumeRemainingSharedCapacity = new MultiPartitionRateLimitRequest(
            "tenant-only:alpha",
            permitCount: 9,
            [shared]);
        Assert.Equal(
            MultiPartitionRateLimitOutcome.Acquired,
            (await limiter.AcquireAsync(consumeRemainingSharedCapacity)).Outcome);
    }

    [Fact]
    public async Task Concurrent_acquisitions_never_exceed_the_partition_limit()
    {
        var limiter = new InMemoryMultiPartitionRateLimiter(
            new MutableClock(WindowStart));
        var request = new MultiPartitionRateLimitRequest(
            "adapter-ingress:alpha",
            permitCount: 1,
            [Partition("tenant:alpha", permitLimit: 100)]);

        Task<MultiPartitionRateLimitDecision>[] attempts =
            Enumerable.Range(0, 250)
                .Select(_ => limiter.AcquireAsync(request).AsTask())
                .ToArray();
        MultiPartitionRateLimitDecision[] decisions =
            await Task.WhenAll(attempts);

        Assert.Equal(
            100,
            decisions.Count(decision =>
                decision.Outcome == MultiPartitionRateLimitOutcome.Acquired));
        Assert.Equal(
            150,
            decisions.Count(decision =>
                decision.Outcome == MultiPartitionRateLimitOutcome.Rejected));
    }

    [Fact]
    public async Task In_memory_registration_is_idempotent_and_identifies_local_scope()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddInMemoryRateLimiting();
        builder.AddInMemoryRateLimiting();

        await using ServiceProvider provider =
            builder.Services.BuildServiceProvider();
        IRateLimitProviderRegistration registration =
            provider.GetRequiredService<IRateLimitProviderRegistration>();

        Assert.Equal("in-memory", registration.ProviderName);
        Assert.False(registration.IsDistributed);
        Assert.Same(
            provider.GetRequiredService<IMultiPartitionRateLimiter>(),
            provider.GetRequiredService<IMultiPartitionRateLimiter>());
    }

    [Fact]
    public void A_second_provider_is_rejected_during_composition()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:redis"] = "localhost:6379";
        builder.AddInMemoryRateLimiting();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => builder.AddRedisRateLimiting());

        Assert.Contains("Only one", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RateLimiting:Redis:ConnectionName", "redis value", "ConnectionName")]
    [InlineData("RateLimiting:Redis:InstanceName", "gma value", "InstanceName")]
    [InlineData("ConnectionStrings:redis", "localhost:6379,connectTimeout=abc", "valid Redis")]
    public void Redis_provider_rejects_invalid_settings_during_composition(
        string setting,
        string value,
        string expectedFailure)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:redis"] = "localhost:6379";
        builder.Configuration[setting] = value;

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => builder.AddRedisRateLimiting());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Redis_registration_is_idempotent_and_identifies_distributed_scope()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:redis"] = "localhost:6379";

        builder.AddRedisRateLimiting();
        builder.AddRedisRateLimiting();

        await using ServiceProvider provider =
            builder.Services.BuildServiceProvider();
        IRateLimitProviderRegistration registration =
            provider.GetRequiredService<IRateLimitProviderRegistration>();

        Assert.Equal("redis", registration.ProviderName);
        Assert.True(registration.IsDistributed);
        Assert.Single(
            builder.Services,
            descriptor =>
                descriptor.ServiceType.Name ==
                "RedisRateLimitingRegistrationMarker");
    }

    [Fact]
    public void Redis_keys_hash_caller_identities_and_share_atomic_group_slot()
    {
        var formatter = new RedisRateLimitStorageKeyFormatter(
            Options.Create(new RedisRateLimitingOptions()),
            Options.Create(new ApplicationIdentityOptions
            {
                Namespace = "gma-tests"
            }),
            new TestHostEnvironment());

        RedisKey first = formatter.Format(
            "tenant-secret",
            Partition("credential-secret", permitLimit: 10));
        RedisKey second = formatter.Format(
            "tenant-secret",
            Partition("tenant-secret", permitLimit: 100));
        string firstText = first.ToString();
        string secondText = second.ToString();

        Assert.DoesNotContain("tenant-secret", firstText, StringComparison.Ordinal);
        Assert.DoesNotContain("credential-secret", firstText, StringComparison.Ordinal);
        Assert.Equal(ExtractHashTag(firstText), ExtractHashTag(secondText));
        Assert.StartsWith(
            "gma-tests:tests:rate-limit:",
            firstText,
            StringComparison.Ordinal);
    }

    private static FixedWindowRateLimitPartition Partition(
        string identity,
        int permitLimit) =>
        new(identity, permitLimit, TimeSpan.FromMinutes(1));

    private static string ExtractHashTag(string key)
    {
        int start = key.IndexOf('{', StringComparison.Ordinal);
        int end = key.IndexOf('}', start + 1);
        return key[(start + 1)..end];
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Tests";
        public string ApplicationName { get; set; } = "Gma.Framework.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
