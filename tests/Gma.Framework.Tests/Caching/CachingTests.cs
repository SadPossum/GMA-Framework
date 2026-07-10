namespace Gma.Framework.Tests;

using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Framework.Cqrs;
using Gma.Framework.Caching;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Observability;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Results;
using Gma.Framework.Runtime;
using Gma.Framework.Caching.Infrastructure;
using Gma.Framework.Cqrs.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
[Collection(MetricsTestGroupDefinition.Name)]
public sealed class CachingTests
{
    [Fact]
    public async Task Disabled_cache_always_invokes_source()
    {
        await using ServiceProvider provider = BuildProvider(enabled: false);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey key = CacheKey.Global("catalog", "product", "42");
        int calls = 0;

        int first = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls));
        int second = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls));

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Disabled_cache_still_validates_read_inputs()
    {
        await using ServiceProvider provider = BuildProvider(enabled: false);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey key = CacheKey.Global("catalog", "product", "42");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            cache.GetOrCreateAsync<int>(null!, Factory).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            cache.GetOrCreateAsync<int>(key, null!).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.GetOrCreateAsync(key, Factory, tags: [null!]).AsTask());

        static ValueTask<int> Factory(CancellationToken _) => ValueTask.FromResult(42);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tenant_scoped_cache_keys_require_active_tenant_even_when_cache_is_disabled(bool enabled)
    {
        MutableTenantContext tenant = new() { TenantId = null };
        await using ServiceProvider provider = BuildProvider(enabled: enabled, tenantContext: tenant);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCreateAsync(
                CacheKey.Scoped("catalog", "product", "42"),
                _ => ValueTask.FromResult(42)).AsTask());
    }

    [Fact]
    public async Task Tenant_scoped_cache_keys_require_tenant_cache_bridge()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Caching:Enabled"] = "false";
        builder.AddCachingInfrastructure();
        await using ServiceProvider provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCreateAsync(
                CacheKey.Scoped("catalog", "product", "42"),
                _ => ValueTask.FromResult(42)).AsTask());

        Assert.Contains("Gma.Framework.Tenancy.Caching", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tenant_context_is_normalized_before_formatting_physical_cache_key()
    {
        MutableTenantContext tenant = new() { TenantId = " alpha " };
        await using ServiceProvider provider = BuildProvider(tenantContext: tenant);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey key = CacheKey.Scoped("catalog", "product", "42");
        int calls = 0;

        int first = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls));
        tenant.TenantId = "alpha";
        int second = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls));

        Assert.Equal(first, second);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_custom_tenant_ids_are_rejected_before_cache_backend_access(bool enabled)
    {
        MutableTenantContext tenant = new() { TenantId = "tenant alpha" };
        await using ServiceProvider provider = BuildProvider(enabled: enabled, tenantContext: tenant);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        ICacheStore cacheStore = scope.ServiceProvider.GetRequiredService<ICacheStore>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.GetOrCreateAsync(
                CacheKey.Scoped("catalog", "product", "42"),
                _ => ValueTask.FromResult(42)).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cacheStore.RemoveAsync(CacheKey.Scoped("catalog", "product", "42"), CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cacheStore.RemoveByTagAsync(CacheTag.Scoped("catalog", "products"), CancellationToken.None).AsTask());
    }

    [Fact]
    public void Cache_formatter_normalizes_storage_prefix_and_environment_name()
    {
        var formatter = new CacheKeyFormatter(
            new FixedCacheScopeValueResolver("alpha"),
            new MutableHostEnvironment { EnvironmentName = " RedisTests_1 " },
            Options.Create(new CachingOptions { KeyPrefix = " GMA-DEV_1 " }),
            Options.Create(new ApplicationIdentityOptions()));

        string physicalKey = formatter.Format(CacheKey.Scoped("catalog", "product", "42"));

        Assert.Equal("gma-dev_1:redistests_1:catalog:scope:alpha:product:42", physicalKey);
    }

    [Fact]
    public void Cache_formatter_uses_application_namespace_when_storage_prefix_is_not_configured()
    {
        var formatter = new CacheKeyFormatter(
            new FixedCacheScopeValueResolver("alpha"),
            new MutableHostEnvironment { EnvironmentName = " RedisTests " },
            Options.Create(new CachingOptions()),
            Options.Create(new ApplicationIdentityOptions { Namespace = "acme-orders" }));

        string physicalKey = formatter.Format(CacheKey.Scoped("catalog", "product", "42"));

        Assert.Equal("acme-orders:redistests:catalog:scope:alpha:product:42", physicalKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_environment_names_are_rejected_before_cache_backend_access(bool enabled)
    {
        await using ServiceProvider provider = BuildProvider(enabled: enabled, environmentName: "Local Development");
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        int calls = 0;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.GetOrCreateAsync(
                CacheKey.Global("catalog", "product", "42"),
                _ => ValueTask.FromResult(++calls)).AsTask());

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Physical_key_limit_errors_are_not_treated_as_backend_fail_open()
    {
        await using ServiceProvider provider = BuildProvider(maximumKeyLength: 8);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        int calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCreateAsync(
                CacheKey.Global("catalog", "product", "42"),
                _ => ValueTask.FromResult(++calls)).AsTask());

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tenant_scoped_invalidations_require_active_tenant_even_when_cache_is_disabled(bool enabled)
    {
        MutableTenantContext tenant = new() { TenantId = null };
        await using ServiceProvider provider = BuildProvider(enabled: enabled, tenantContext: tenant);
        using IServiceScope scope = provider.CreateScope();
        ICacheStore cacheStore = scope.ServiceProvider.GetRequiredService<ICacheStore>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cacheStore.RemoveAsync(CacheKey.Scoped("catalog", "product", "42"), CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cacheStore.RemoveByTagAsync(CacheTag.Scoped("catalog", "products"), CancellationToken.None).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalidation_key_limit_errors_are_not_treated_as_backend_fail_open(bool enabled)
    {
        await using ServiceProvider provider = BuildProvider(enabled: enabled, maximumKeyLength: 8);
        using IServiceScope scope = provider.CreateScope();
        ICacheStore cacheStore = scope.ServiceProvider.GetRequiredService<ICacheStore>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cacheStore.RemoveAsync(CacheKey.Global("catalog", "product", "42"), CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cacheStore.RemoveByTagAsync(CacheTag.Global("catalog", "products"), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Memory_cache_hits_after_first_read()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey key = CacheKey.Global("catalog", "product", "42");
        int calls = 0;

        int first = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls));
        int second = await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls));

        Assert.Equal(first, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Concurrent_reads_share_one_source_execution()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey key = CacheKey.Global("catalog", "product", "stampede");
        int calls = 0;

        Task<int>[] reads = Enumerable.Range(0, 16)
            .Select(_ => cache.GetOrCreateAsync(
                key,
                async token =>
                {
                    Interlocked.Increment(ref calls);
                    await Task.Delay(50, token);
                    return 42;
                }).AsTask())
            .ToArray();

        int[] values = await Task.WhenAll(reads);

        Assert.All(values, value => Assert.Equal(42, value));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Entry_expires_according_to_policy()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey key = CacheKey.Global("catalog", "short-lived");
        CacheEntryPolicy policy = new(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(40));
        int calls = 0;

        await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls), policy);
        await Task.Delay(150);
        await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls), policy);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Tenant_keys_are_isolated_while_global_keys_are_shared()
    {
        MutableTenantContext tenant = new() { TenantId = "alpha" };
        await using ServiceProvider provider = BuildProvider(tenantContext: tenant);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey tenantKey = CacheKey.Scoped("catalog", "product", "42");
        CacheKey globalKey = CacheKey.Global("catalog", "settings");
        int tenantCalls = 0;
        int globalCalls = 0;

        int alpha = await cache.GetOrCreateAsync(tenantKey, _ => ValueTask.FromResult(++tenantCalls));
        int globalAlpha = await cache.GetOrCreateAsync(globalKey, _ => ValueTask.FromResult(++globalCalls));
        tenant.TenantId = "beta";
        int beta = await cache.GetOrCreateAsync(tenantKey, _ => ValueTask.FromResult(++tenantCalls));
        int globalBeta = await cache.GetOrCreateAsync(globalKey, _ => ValueTask.FromResult(++globalCalls));

        Assert.Equal(1, alpha);
        Assert.Equal(2, beta);
        Assert.Equal(globalAlpha, globalBeta);
        Assert.Equal(1, globalCalls);
    }

    [Fact]
    public async Task Key_and_tag_invalidations_remove_entries()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        ICacheInvalidationQueue queue = scope.ServiceProvider.GetRequiredService<ICacheInvalidationQueue>();
        ICacheInvalidationQueueFlusher flusher = scope.ServiceProvider.GetRequiredService<ICacheInvalidationQueueFlusher>();
        CacheKey key = CacheKey.Global("catalog", "product", "42");
        CacheTag tag = CacheTag.Global("catalog", "products");
        int calls = 0;

        await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls), tags: [tag]);
        queue.Remove(key);
        await flusher.FlushAsync(CancellationToken.None);
        await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls), tags: [tag]);
        queue.RemoveByTag(tag);
        await flusher.FlushAsync(CancellationToken.None);
        await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult(++calls), tags: [tag]);

        Assert.Equal(3, calls);
    }

    [Fact]
    public void Cache_keys_and_tags_normalize_and_validate_segments()
    {
        CacheKey key = CacheKey.Global("Catalog", "Product", " 42 ");
        CacheTag tag = CacheTag.Scoped("Catalog", "Products", " Featured ");

        Assert.Equal("catalog", key.Module);
        Assert.Equal("product", key.Entry);
        Assert.Equal(["42"], key.Segments);
        Assert.Equal("catalog", tag.Module);
        Assert.Equal("products", tag.Entry);
        Assert.Equal(["Featured"], tag.Segments);

        Assert.Throws<ArgumentException>(() => CacheKey.Global("catalog.items", "product"));
        Assert.Throws<ArgumentException>(() => CacheTag.Global("catalog_items", "products"));
        Assert.Throws<ArgumentException>(() => CacheKey.Global("catalog", "product", " "));
        Assert.Throws<ArgumentException>(() => CacheKey.Global("catalog", "product", "bad segment"));
        Assert.Throws<ArgumentException>(() => CacheKey.Global("catalog", "product", $"bad{char.MinValue}segment"));
        Assert.Throws<ArgumentException>(() => CacheKey.Global(
            "catalog",
            "product",
            new string('x', CacheKey.SegmentMaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CacheTag.Global(
            "catalog",
            "products",
            Enumerable.Range(0, CacheTag.MaxSegments + 1).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray()));
    }

    [Fact]
    public async Task Invalidation_runs_after_successful_unit_of_work_commit()
    {
        List<string> order = [];
        RecordingUnitOfWork unitOfWork = new(order);
        RecordingInvalidationFlusher flusher = new(order);
        CommandUnitOfWorkBehavior<TestCommand, Unit> unitOfWorkBehavior = new([unitOfWork]);
        CacheInvalidationCommandBehavior<TestCommand, Unit> invalidationBehavior = new(flusher);

        Result<Unit> result = await invalidationBehavior.HandleAsync(
            new TestCommand(),
            () => unitOfWorkBehavior.HandleAsync(
                new TestCommand(),
                () =>
                {
                    order.Add("handler");
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["handler", "commit", "invalidate"], order);
    }

    [Fact]
    public async Task Invalidation_does_not_run_for_failed_command_or_commit()
    {
        List<string> failedCommandOrder = [];
        CacheInvalidationCommandBehavior<TestCommand, Unit> failedCommandBehavior = new(
            new RecordingInvalidationFlusher(failedCommandOrder));

        Result<Unit> failed = await failedCommandBehavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(Result.Failure<Unit>(new Error("Test.Failed", "Expected failure."))),
            CancellationToken.None);

        Assert.True(failed.IsFailure);
        Assert.Empty(failedCommandOrder);

        List<string> failedCommitOrder = [];
        RecordingUnitOfWork unitOfWork = new(failedCommitOrder, throwOnCommit: true);
        CommandUnitOfWorkBehavior<TestCommand, Unit> unitOfWorkBehavior = new([unitOfWork]);
        CacheInvalidationCommandBehavior<TestCommand, Unit> failedCommitBehavior = new(
            new RecordingInvalidationFlusher(failedCommitOrder));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failedCommitBehavior.HandleAsync(
            new TestCommand(),
            () => unitOfWorkBehavior.HandleAsync(
                new TestCommand(),
                () =>
                {
                    failedCommitOrder.Add("handler");
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None),
            CancellationToken.None));

        Assert.Equal(["handler", "commit"], failedCommitOrder);
    }

    [Fact]
    public async Task Invalidation_flusher_failure_does_not_fail_committed_command()
    {
        List<string> order = [];
        RecordingUnitOfWork unitOfWork = new(order);
        CommandUnitOfWorkBehavior<TestCommand, Unit> unitOfWorkBehavior = new([unitOfWork]);
        CacheInvalidationCommandBehavior<TestCommand, Unit> invalidationBehavior = new(
            new ThrowingInvalidationFlusher(order));

        Result<Unit> result = await invalidationBehavior.HandleAsync(
            new TestCommand(),
            () => unitOfWorkBehavior.HandleAsync(
                new TestCommand(),
                () =>
                {
                    order.Add("handler");
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["handler", "commit", "invalidate"], order);
    }

    [Fact]
    public async Task Caching_cqrs_bridge_registers_invalidation_before_unit_of_work()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        Type[] behaviorTypes = scope.ServiceProvider
            .GetServices<ICommandPipelineBehavior<TestCommand, Unit>>()
            .Select(behavior => behavior.GetType())
            .ToArray();

        Assert.Equal(
            [
                typeof(ValidationCommandBehavior<TestCommand, Unit>),
                typeof(LoggingCommandBehavior<TestCommand, Unit>),
                typeof(CacheInvalidationCommandBehavior<TestCommand, Unit>),
                typeof(CommandUnitOfWorkBehavior<TestCommand, Unit>)
            ],
            behaviorTypes);
    }

    [Fact]
    public async Task Caching_infrastructure_does_not_register_cqrs_pipeline_behavior()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Caching:Enabled"] = "false";
        builder.Configuration["Tenancy:Enabled"] = "false";
        builder.AddCachingInfrastructure();
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();

        Assert.Empty(provider.GetServices<ICommandPipelineBehavior<TestCommand, Unit>>());
    }

    [Fact]
    public async Task Backend_failure_falls_back_to_source()
    {
        List<string> instruments = [];
        using MeterListener listener = new()
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == ObservabilityMeterNames.Caching)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => instruments.Add(instrument.Name));
        listener.Start();
        await using ServiceProvider provider = BuildProvider(
            providerName: "Redis",
            addThrowingLogger: true,
            configureServices: AddThrowingDistributedCache);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        int calls = 0;

        int value = await cache.GetOrCreateAsync(
            CacheKey.Global("catalog", "backend-failure"),
            _ => ValueTask.FromResult(++calls));

        Assert.Equal(1, value);
        Assert.Equal(1, calls);
        Assert.Contains(ObservabilityInstrumentNames.CacheBackendFailures, instruments);
    }

    [Fact]
    public async Task Disabled_cache_succeeds_when_metric_listener_throws()
    {
        using MeterListener listener = CreateThrowingCachingMeterListener();
        await using ServiceProvider provider = BuildProvider(enabled: false);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();

        int value = await cache.GetOrCreateAsync(
            CacheKey.Global("catalog", "metrics-throw"),
            _ => ValueTask.FromResult(42));

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task Backend_failure_still_falls_back_when_metric_listener_throws()
    {
        using MeterListener listener = CreateThrowingCachingMeterListener();
        await using ServiceProvider provider = BuildProvider(
            providerName: "Redis",
            configureServices: AddThrowingDistributedCache);
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();

        int value = await cache.GetOrCreateAsync(
            CacheKey.Global("catalog", "backend-metrics-throw"),
            _ => ValueTask.FromResult(42));

        Assert.Equal(42, value);
    }

    [Fact]
    public async Task Invalidation_backend_failure_does_not_fail_flush()
    {
        await using ServiceProvider provider = BuildProvider(
            providerName: "Redis",
            addThrowingLogger: true,
            configureServices: AddThrowingDistributedCache);
        using IServiceScope scope = provider.CreateScope();
        ICacheInvalidationQueue queue = scope.ServiceProvider.GetRequiredService<ICacheInvalidationQueue>();
        ICacheInvalidationQueueFlusher flusher = scope.ServiceProvider.GetRequiredService<ICacheInvalidationQueueFlusher>();

        queue.Remove(CacheKey.Global("catalog", "backend-failure"));
        queue.RemoveByTag(CacheTag.Global("catalog", "backend-failure"));

        await flusher.FlushAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Invalidation_backend_failure_still_fails_open_when_metric_listener_throws()
    {
        using MeterListener listener = CreateThrowingCachingMeterListener();
        await using ServiceProvider provider = BuildProvider(
            providerName: "Redis",
            configureServices: AddThrowingDistributedCache);
        using IServiceScope scope = provider.CreateScope();
        ICacheInvalidationQueue queue = scope.ServiceProvider.GetRequiredService<ICacheInvalidationQueue>();
        ICacheInvalidationQueueFlusher flusher = scope.ServiceProvider.GetRequiredService<ICacheInvalidationQueueFlusher>();

        queue.Remove(CacheKey.Global("catalog", "backend-metrics-throw"));
        queue.RemoveByTag(CacheTag.Global("catalog", "backend-metrics-throw"));

        await flusher.FlushAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Hybrid_cache_metrics_logger_ignores_throwing_metric_listener()
    {
        using MeterListener listener = CreateThrowingCachingMeterListener();
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CacheMetrics metrics = new(
            provider.GetRequiredService<IMeterFactory>(),
            Options.Create(new ApplicationIdentityOptions()));
        using HybridCacheMetricsLoggerProvider loggerProvider = new(
            metrics,
            Options.Create(new CachingOptions { Provider = CacheProvider.Redis }));
        ILogger logger = loggerProvider.CreateLogger("Microsoft.Extensions.Caching.Hybrid.HybridCache");

        logger.Log(
            LogLevel.Warning,
            new EventId(6, "BackendReadFailure"),
            "cache backend failed",
            exception: null,
            static (state, _) => state);
    }

    [Fact]
    public async Task Invalidation_queue_keeps_unflushed_entries_after_cancellation()
    {
        CancelSecondInvalidationCacheStore cacheStore = new();
        CacheInvalidationQueue queue = new(cacheStore);
        CacheKey first = CacheKey.Global("catalog", "first");
        CacheKey second = CacheKey.Global("catalog", "second");
        CacheTag tag = CacheTag.Global("catalog", "products");

        queue.Remove(first);
        queue.Remove(second);
        queue.RemoveByTag(tag);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            queue.FlushAsync(CancellationToken.None).AsTask());
        await queue.FlushAsync(CancellationToken.None);

        Assert.Equal([first, second], cacheStore.RemovedKeys);
        Assert.Equal([tag], cacheStore.RemovedTags);
    }

    [Fact]
    public async Task Redis_mode_without_adapter_fails_startup_validation()
    {
        await using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        CachingStartupValidator validator = new(
            provider,
            Options.Create(new CachingOptions { Enabled = true, Provider = CacheProvider.Redis }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validator.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Redis_mode_without_adapter_fails_when_application_cache_is_resolved()
    {
        await using ServiceProvider provider = BuildProvider(providerName: "Redis");
        using IServiceScope scope = provider.CreateScope();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            scope.ServiceProvider.GetRequiredService<IApplicationCache>());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("AddRedisCaching()", StringComparison.Ordinal));
    }

    [Fact]
    public void Redis_mode_without_adapter_fails_options_startup_validation()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Caching:Enabled"] = "true";
        builder.Configuration["Caching:Provider"] = "Redis";
        builder.AddTenancyInfrastructure();
        builder.AddTenantCaching();
        builder.AddCachingCqrs();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("AddRedisCaching()", StringComparison.Ordinal));
    }

    [Fact]
    public void Redis_adapter_rejects_unknown_enabled_provider_at_composition()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Caching:Enabled"] = "true";
        builder.Configuration["Caching:Provider"] = "Unknown";

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            builder.AddRedisCaching());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("Caching:Provider", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Caching:Redis:ConnectionName", "redis value", "ConnectionName")]
    [InlineData("Caching:Redis:InstanceName", "gma value", "InstanceName")]
    [InlineData("ConnectionStrings:redis", "localhost:6379,connectTimeout=abc", "valid Redis")]
    public void Redis_adapter_rejects_invalid_adapter_settings_at_composition(
        string setting,
        string value,
        string expectedFailure)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Caching:Enabled"] = "true";
        builder.Configuration["Caching:Provider"] = "Redis";
        builder.Configuration["ConnectionStrings:redis"] = "localhost:6379";
        builder.Configuration[setting] = value;

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            builder.AddRedisCaching());

        Assert.Contains(exception.Failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Fact]
    public void Redis_adapter_registration_is_idempotent()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Caching:Enabled"] = "true";
        builder.Configuration["Caching:Provider"] = "Redis";
        builder.Configuration["ConnectionStrings:redis"] = "localhost:6379";

        builder.AddRedisCaching();
        builder.AddRedisCaching();

        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IDistributedCache));
        Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<RedisCachingOptions>));
        Assert.Single(builder.Services, HasService<IValidateOptions<RedisCachingOptions>, RedisCachingOptionsValidator>());
        Assert.Single(builder.Services, descriptor => descriptor.ServiceType.Name == "RedisCachingRegistrationMarker");
    }

    [Fact]
    public async Task Source_exceptions_and_cancellation_propagate()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();
        CacheKey exceptionKey = CacheKey.Global("catalog", "exception");
        InvalidOperationException expected = new("source failed");

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCreateAsync<int>(exceptionKey, _ => ValueTask.FromException<int>(expected)).AsTask());
        Assert.Same(expected, actual);

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.GetOrCreateAsync(
                CacheKey.Global("catalog", "cancelled"),
                _ => ValueTask.FromResult(1),
                cancellationToken: cancellation.Token).AsTask());
    }

    [Fact]
    public async Task Cache_metrics_use_only_bounded_tags()
    {
        List<IReadOnlyDictionary<string, object?>> measurements = [];
        using MeterListener listener = new()
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == ObservabilityMeterNames.Caching)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            measurements.Add(tags.ToArray().ToDictionary(item => item.Key, item => item.Value)));
        listener.Start();
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IApplicationCache cache = scope.ServiceProvider.GetRequiredService<IApplicationCache>();

        await cache.GetOrCreateAsync(
            CacheKey.Scoped("catalog", "product", "high-cardinality-key"),
            _ => ValueTask.FromResult(42));

        IReadOnlyDictionary<string, object?> tags = Assert.Single(measurements);
        Assert.Equal(
            [ObservabilityTagNames.Module, ObservabilityTagNames.Operation, ObservabilityTagNames.Provider, ObservabilityTagNames.Result],
            tags.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(tags.Values, value => Equals(value, "high-cardinality-key"));
    }

    private static ServiceProvider BuildProvider(
        bool enabled = true,
        string providerName = "Memory",
        MutableTenantContext? tenantContext = null,
        bool addThrowingLogger = false,
        Action<IServiceCollection>? configureServices = null,
        int? maximumKeyLength = null,
        string environmentName = "Tests")
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = environmentName;
        builder.Configuration["Caching:Enabled"] = enabled.ToString();
        builder.Configuration["Caching:Provider"] = providerName;
        builder.Configuration["Caching:DefaultDistributedExpiration"] = "00:05:00";
        builder.Configuration["Caching:DefaultLocalExpiration"] = "00:00:30";
        builder.Configuration["Caching:MaximumKeyLength"] = (maximumKeyLength ?? 1024).ToString(CultureInfo.InvariantCulture);
        builder.Configuration["Tenancy:Enabled"] = "false";

        if (addThrowingLogger)
        {
            builder.Logging.AddProvider(new ThrowingLoggerProvider());
        }

        configureServices?.Invoke(builder.Services);
        builder.AddTenancyInfrastructure();
        builder.AddTenantCaching();
        builder.AddCachingCqrs();

        if (tenantContext is not null)
        {
            builder.Services.Replace(ServiceDescriptor.Singleton<ITenantContext>(tenantContext));
        }

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static void AddThrowingDistributedCache(IServiceCollection services)
    {
        services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
        services.AddSingleton<IDistributedCacheAdapterRegistration, TestDistributedCacheAdapterRegistration>();
    }

    private static Predicate<ServiceDescriptor> HasService<TService, TImplementation>() =>
        descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation);

    private static MeterListener CreateThrowingCachingMeterListener()
    {
        MeterListener listener = new()
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == ObservabilityMeterNames.Caching)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
            throw new InvalidOperationException("Metric listener unavailable."));
        listener.SetMeasurementEventCallback<double>((_, _, _, _) =>
            throw new InvalidOperationException("Metric listener unavailable."));
        listener.Start();

        return listener;
    }

    private sealed record TestCommand : ITransactionalCommand<Unit>;

    private sealed class MutableTenantContext : ITenantContext
    {
        public bool IsEnabled => true;
        public string? TenantId { get; set; }
    }

    private sealed class FixedCacheScopeValueResolver(string tenantScopeValue) : ICacheScopeValueResolver
    {
        public string Resolve(CacheScope scope) =>
            scope switch
            {
                CacheScope.Global => "global",
                CacheScope.Scope => tenantScopeValue,
                _ => throw new InvalidOperationException($"Unsupported cache scope '{scope}'.")
            };
    }

    private sealed class MutableHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Tests";
        public string ApplicationName { get; set; } = "Gma.Framework.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingUnitOfWork(List<string> order, bool throwOnCommit = false) : IUnitOfWork
    {
        public string ModuleName => "framework";

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            order.Add("commit");

            if (throwOnCommit)
            {
                throw new InvalidOperationException("Commit failed.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInvalidationFlusher(List<string> order) : ICacheInvalidationQueueFlusher
    {
        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            order.Add("invalidate");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingInvalidationFlusher(List<string> order) : ICacheInvalidationQueueFlusher
    {
        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            order.Add("invalidate");
            throw new InvalidOperationException("Invalidation backend failed.");
        }
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Redis unavailable.");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromException<byte[]?>(new InvalidOperationException("Redis unavailable."));
        public void Refresh(string key) => throw new InvalidOperationException("Redis unavailable.");
        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Task.FromException(new InvalidOperationException("Redis unavailable."));
        public void Remove(string key) => throw new InvalidOperationException("Redis unavailable.");
        public Task RemoveAsync(string key, CancellationToken token = default) =>
            Task.FromException(new InvalidOperationException("Redis unavailable."));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("Redis unavailable.");
        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            Task.FromException(new InvalidOperationException("Redis unavailable."));
    }

    private sealed class TestDistributedCacheAdapterRegistration : IDistributedCacheAdapterRegistration;

    private sealed class CancelSecondInvalidationCacheStore : ICacheStore
    {
        private int calls;

        public List<CacheKey> RemovedKeys { get; } = [];
        public List<CacheTag> RemovedTags { get; } = [];

        public ValueTask RemoveAsync(CacheKey key, CancellationToken cancellationToken)
        {
            this.calls++;
            if (this.calls == 2)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            this.RemovedKeys.Add(key);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveByTagAsync(CacheTag tag, CancellationToken cancellationToken)
        {
            this.RemovedTags.Add(tag);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ThrowingLogger();

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
