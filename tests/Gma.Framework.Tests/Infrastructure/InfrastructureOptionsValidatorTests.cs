namespace Gma.Framework.Tests;

using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;
using Gma.Framework.Caching;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.Tenancy;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Caching.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Tenancy.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InfrastructureOptionsValidatorTests
{
    [Fact]
    public void Tenant_id_normalizer_trims_and_rejects_invalid_values()
    {
        Assert.Equal("tenant-a", ScopeIds.Normalize(" tenant-a "));
        Assert.Equal("Tenant-A", ScopeIds.Normalize(" Tenant-A "));
        Assert.False(ScopeIds.TryNormalize(" ", out _));
        Assert.False(ScopeIds.TryNormalize("tenant a", out _));
        Assert.False(ScopeIds.TryNormalize("tenant\tid", out _));
        Assert.False(ScopeIds.TryNormalize($"tenant{char.MinValue}id", out _));
        Assert.False(ScopeIds.TryNormalize(new string('x', ScopeIds.MaxLength + 1), out _));
        Assert.Throws<ArgumentException>(() => ScopeIds.Normalize(string.Empty));
    }

    [Fact]
    public void Null_tenant_context_normalizes_default_and_set_values()
    {
        NullTenantContext context = new(Options.Create(new TenantOptions
        {
            LocalDefaultTenantId = " default "
        }));

        Assert.Equal("default", context.TenantId);

        context.SetTenant(" tenant-a ");

        Assert.Equal("tenant-a", context.TenantId);
        context.ClearTenant();
        Assert.Equal("default", context.TenantId);
        Assert.Throws<ArgumentException>(() => context.SetTenant(" "));
    }

    [Fact]
    public void Application_identity_validator_accepts_default_and_custom_namespace()
    {
        var validator = new ApplicationIdentityOptionsValidator();

        ValidateOptionsResult defaultResult = validator.Validate(name: null, new ApplicationIdentityOptions());
        ValidateOptionsResult customResult = validator.Validate(
            name: null,
            new ApplicationIdentityOptions
            {
                DisplayName = "Acme Orders",
                Namespace = "acme-orders"
            });

        Assert.True(defaultResult.Succeeded);
        Assert.True(customResult.Succeeded);
        Assert.Equal("acme-orders", new ApplicationIdentityOptions { Namespace = " Acme-Orders " }.EffectiveNamespace);
    }

    [Theory]
    [InlineData("", "app", "Namespace")]
    [InlineData("gma.value", "app", "Namespace")]
    [InlineData("gma_value", "app", "Namespace")]
    [InlineData("gma value", "app", "Namespace")]
    [InlineData("gma", "", "DisplayName")]
    public void Application_identity_validator_rejects_invalid_settings(
        string applicationNamespace,
        string displayName,
        string expectedFailure)
    {
        var validator = new ApplicationIdentityOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new ApplicationIdentityOptions
            {
                Namespace = applicationNamespace,
                DisplayName = displayName
            });

        AssertFailure(result, expectedFailure);
    }

    [Fact]
    public void Shared_non_persisted_semantic_enums_keep_unknown_zero()
    {
        Type[] sharedSemanticEnums =
        [
            typeof(DatabaseProvider),
            typeof(CacheProvider),
            typeof(CacheScope),
            typeof(InboxProcessStatus),
            typeof(AdminOperationExecutionStatus)
        ];

        string[] offenders = sharedSemanticEnums
            .Where(type => !string.Equals(Enum.GetName(type, 0), "Unknown", StringComparison.Ordinal))
            .Select(type => type.FullName ?? type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Persistence_validator_accepts_default_sql_server_settings_when_connection_exists()
    {
        var validator = new PersistenceOptionsValidator(CreateConfiguration(
            ("ConnectionStrings:SqlServer", "Server=localhost;Database=gma;Trusted_Connection=True")));

        ValidateOptionsResult result = validator.Validate(name: null, new PersistenceOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Persistence_validator_accepts_postgresql_when_connection_exists()
    {
        var validator = new PersistenceOptionsValidator(CreateConfiguration(
            ("ConnectionStrings:PostgreSql", "Host=localhost;Database=gma")));

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new PersistenceOptions { Provider = DatabaseProvider.PostgreSql });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(DatabaseProvider.Unknown, "Provider")]
    [InlineData((DatabaseProvider)42, "Provider")]
    [InlineData(DatabaseProvider.SqlServer, "SqlServer")]
    [InlineData(DatabaseProvider.PostgreSql, "PostgreSql")]
    public void Persistence_validator_rejects_invalid_provider_or_missing_connection(
        DatabaseProvider provider,
        string expectedFailure)
    {
        var validator = new PersistenceOptionsValidator(CreateConfiguration());

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new PersistenceOptions { Provider = provider });

        AssertFailure(result, expectedFailure);
    }

    [Fact]
    public void Persistence_options_registration_rejects_invalid_settings_before_service_mutation()
    {
        ServiceCollection services = new();
        Microsoft.Extensions.Configuration.ConfigurationManager configuration = CreateConfiguration(
            ("Persistence:Provider", "PostgreSql"));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddPersistenceOptions(configuration));

        Assert.Contains(exception.Failures, failure => failure.Contains("PostgreSql", StringComparison.Ordinal));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IValidateOptions<PersistenceOptions>));
    }

    [Fact]
    public void Configured_db_context_provider_rejects_invalid_settings_before_provider_registration()
    {
        DbContextOptionsBuilder options = new();
        Microsoft.Extensions.Configuration.ConfigurationManager configuration = CreateConfiguration(
            ("Persistence:Provider", "PostgreSql"));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            options.UseConfiguredProvider(
                configuration,
                "Gma.Framework.Tests.SqlServerMigrations",
                "Gma.Framework.Tests.PostgreSqlMigrations"));

        Assert.Contains(exception.Failures, failure => failure.Contains("PostgreSql", StringComparison.Ordinal));
        Assert.Empty(options.Options.Extensions);
    }

    [Fact]
    public void Caching_validator_accepts_default_settings()
    {
        var validator = new CachingOptionsValidator();

        ValidateOptionsResult result = validator.Validate(name: null, new CachingOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Caching_validator_accepts_custom_key_prefix()
    {
        var validator = new CachingOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new CachingOptions { KeyPrefix = "GMA-DEV_1" });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(CacheProvider.Unknown, "Provider")]
    [InlineData((CacheProvider)42, "Provider")]
    public void Caching_validator_rejects_invalid_provider(CacheProvider provider, string expectedFailure)
    {
        var validator = new CachingOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new CachingOptions { Provider = provider });

        AssertFailure(result, expectedFailure);
    }

    [Theory]
    [InlineData("gma dev")]
    [InlineData("gma:dev")]
    [InlineData("gma.dev")]
    public void Caching_validator_rejects_invalid_key_prefix(string keyPrefix)
    {
        var validator = new CachingOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new CachingOptions { KeyPrefix = keyPrefix });

        AssertFailure(result, "KeyPrefix");
    }

    [Fact]
    public void Caching_validator_rejects_overlong_key_prefix()
    {
        var validator = new CachingOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new CachingOptions { KeyPrefix = new string('a', CachingOptions.KeyPrefixMaxLength + 1) });

        AssertFailure(result, "KeyPrefix");
    }

    [Fact]
    public void Outbox_validator_accepts_default_settings()
    {
        var validator = new OutboxOptionsValidator();

        ValidateOptionsResult result = validator.Validate(name: null, new OutboxOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Outbox_runtime_values_are_safe_when_validation_is_bypassed()
    {
        OutboxOptions options = new()
        {
            BatchSize = 0,
            PollIntervalMilliseconds = 0,
            LockDurationMilliseconds = 0,
            MaxAttempts = 0
        };

        Assert.Equal(1, options.EffectiveBatchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(1), options.EffectivePollInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(1), options.EffectiveLockDuration);
        Assert.Equal(1, options.EffectiveMaxAttempts);
    }

    [Fact]
    public void Redis_validator_accepts_default_settings_when_connection_exists()
    {
        var validator = new RedisCachingOptionsValidator(CreateConfiguration(
            ("ConnectionStrings:redis", "localhost:6379")));

        ValidateOptionsResult result = validator.Validate(name: null, new RedisCachingOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("redis", "")]
    [InlineData("redis", "gma:")]
    [InlineData("redis-cache", "gma:blue:")]
    public void Redis_validator_accepts_optional_instance_name(
        string connectionName,
        string instanceName)
    {
        var validator = new RedisCachingOptionsValidator(CreateConfiguration(
            ($"ConnectionStrings:{connectionName}", "localhost:6379")));

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new RedisCachingOptions
            {
                ConnectionName = connectionName,
                InstanceName = instanceName
            });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("", "gma:", "localhost:6379", "ConnectionName")]
    [InlineData("redis value", "gma:", "localhost:6379", "ConnectionName")]
    [InlineData("redis", "gma value", "localhost:6379", "InstanceName")]
    [InlineData("redis", "gma\t", "localhost:6379", "InstanceName")]
    [InlineData("missing", "gma:", "", "ConnectionStrings:missing")]
    [InlineData("redis", "gma:", "localhost:6379,connectTimeout=abc", "valid Redis")]
    public void Redis_validator_rejects_invalid_adapter_settings(
        string connectionName,
        string instanceName,
        string connectionString,
        string expectedFailure)
    {
        var validator = new RedisCachingOptionsValidator(CreateConfiguration(
            ("ConnectionStrings:redis", connectionString)));

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new RedisCachingOptions
            {
                ConnectionName = connectionName,
                InstanceName = instanceName
            });

        AssertFailure(result, expectedFailure);
    }

    [Fact]
    public void Redis_validator_rejects_overlong_adapter_names()
    {
        var validator = new RedisCachingOptionsValidator(CreateConfiguration(
            ("ConnectionStrings:redis", "localhost:6379")));

        ValidateOptionsResult connectionNameResult = validator.Validate(
            name: null,
            new RedisCachingOptions
            {
                ConnectionName = new string('r', RedisCachingOptions.ConnectionNameMaxLength + 1),
                InstanceName = "gma:"
            });
        ValidateOptionsResult instanceNameResult = validator.Validate(
            name: null,
            new RedisCachingOptions
            {
                ConnectionName = "redis",
                InstanceName = new string('g', RedisCachingOptions.InstanceNameMaxLength + 1)
            });

        AssertFailure(connectionNameResult, "ConnectionName");
        AssertFailure(instanceNameResult, "InstanceName");
    }

    [Fact]
    public void Tenant_validator_accepts_default_settings()
    {
        var validator = new TenantOptionsValidator();

        ValidateOptionsResult result = validator.Validate(name: null, new TenantOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("", "default", "HeaderName")]
    [InlineData("X Tenant Id", "default", "HeaderName")]
    [InlineData("X-Tenant-Id", "", "LocalDefaultTenantId")]
    [InlineData("X-Tenant-Id", "default tenant", "LocalDefaultTenantId")]
    public void Tenant_validator_rejects_invalid_header_or_default_tenant(
        string headerName,
        string LocalDefaultTenantId,
        string expectedFailure)
    {
        var validator = new TenantOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new TenantOptions
            {
                HeaderName = headerName,
                LocalDefaultTenantId = LocalDefaultTenantId
            });

        AssertFailure(result, expectedFailure);
    }

    [Fact]
    public void Tenant_validator_rejects_too_long_default_tenant()
    {
        var validator = new TenantOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new TenantOptions
            {
                HeaderName = "X-Tenant-Id",
                LocalDefaultTenantId = new string('x', ScopeIds.MaxLength + 1)
            });

        AssertFailure(result, "LocalDefaultTenantId");
    }

    [Theory]
    [InlineData(0, 5000, 60000, 10, "BatchSize")]
    [InlineData(25, 0, 60000, 10, "PollIntervalMilliseconds")]
    [InlineData(25, 5000, 0, 10, "LockDurationMilliseconds")]
    [InlineData(25, 5000, 60000, 0, "MaxAttempts")]
    public void Outbox_validator_rejects_non_positive_runtime_values(
        int batchSize,
        int pollIntervalMilliseconds,
        int lockDurationMilliseconds,
        int maxAttempts,
        string expectedFailure)
    {
        var validator = new OutboxOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new OutboxOptions
            {
                BatchSize = batchSize,
                PollIntervalMilliseconds = pollIntervalMilliseconds,
                LockDurationMilliseconds = lockDurationMilliseconds,
                MaxAttempts = maxAttempts
            });

        AssertFailure(result, expectedFailure);
    }

    [Fact]
    public void Nats_jetstream_validator_accepts_default_settings()
    {
        var validator = new NatsJetStreamOptionsValidator();

        ValidateOptionsResult result = validator.Validate(name: null, new NatsJetStreamOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Nats_jetstream_validator_accepts_missing_stream_name_as_application_identity_default()
    {
        var validator = new NatsJetStreamOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new NatsJetStreamOptions { StreamName = string.Empty });

        Assert.True(result.Succeeded);
        Assert.Equal("ACME_ORDERS_EVENTS", new NatsJetStreamOptions().EffectiveStreamName("acme-orders"));
        Assert.Equal("acme-orders.>", NatsJetStreamOptions.CreateSubjectWildcard("acme-orders"));
    }

    [Theory]
    [InlineData("GMA.EVENTS")]
    [InlineData("GMA*EVENTS")]
    [InlineData("GMA>EVENTS")]
    [InlineData("GMA/EVENTS")]
    [InlineData("GMA\\EVENTS")]
    [InlineData("GMA EVENTS")]
    [InlineData("GMA:EVENTS")]
    [InlineData("NUL")]
    public void Nats_jetstream_validator_rejects_invalid_stream_names(string streamName)
    {
        var validator = new NatsJetStreamOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new NatsJetStreamOptions { StreamName = streamName });

        AssertFailure(result, "StreamName");
    }

    [Fact]
    public void Nats_jetstream_validator_rejects_too_long_stream_name()
    {
        var validator = new NatsJetStreamOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new NatsJetStreamOptions { StreamName = new string('X', NatsStreamNames.MaxLength + 1) });

        AssertFailure(result, "StreamName");
    }

    [Fact]
    public void Nats_consumer_validator_accepts_default_settings()
    {
        var validator = new NatsConsumerOptionsValidator();

        ValidateOptionsResult result = validator.Validate(name: null, new NatsConsumerOptions());

        Assert.True(result.Succeeded);
        Assert.Equal("acme-orders", new NatsConsumerOptions().EffectiveDurablePrefix("acme-orders"));
    }

    [Theory]
    [InlineData("gma.prod", 10, 1000, 30000, 5, 30000, 1000, "DurablePrefix")]
    [InlineData("gma prod", 10, 1000, 30000, 5, 30000, 1000, "DurablePrefix")]
    [InlineData("gma", 0, 1000, 30000, 5, 30000, 1000, "FetchBatchSize")]
    [InlineData("gma", 501, 1000, 30000, 5, 30000, 1000, "FetchBatchSize")]
    [InlineData("gma", 10, 0, 30000, 5, 30000, 1000, "PollInterval")]
    [InlineData("gma", 10, 1000, 0, 5, 30000, 1000, "AckWait")]
    [InlineData("gma", 10, 1000, 30000, 0, 30000, 1000, "MaxDeliver")]
    [InlineData("gma", 10, 1000, 30000, 5, 0, 1000, "HandlerTimeout")]
    [InlineData("gma", 10, 1000, 30000, 5, 30000, 0, "NakDelay")]
    public void Nats_consumer_validator_rejects_invalid_runtime_values(
        string durablePrefix,
        int fetchBatchSize,
        int pollIntervalMilliseconds,
        int ackWaitMilliseconds,
        int maxDeliver,
        int handlerTimeoutMilliseconds,
        int nakDelayMilliseconds,
        string expectedFailure)
    {
        var validator = new NatsConsumerOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            name: null,
            new NatsConsumerOptions
            {
                DurablePrefix = durablePrefix,
                FetchBatchSize = fetchBatchSize,
                PollInterval = TimeSpan.FromMilliseconds(pollIntervalMilliseconds),
                AckWait = TimeSpan.FromMilliseconds(ackWaitMilliseconds),
                MaxDeliver = maxDeliver,
                HandlerTimeout = TimeSpan.FromMilliseconds(handlerTimeoutMilliseconds),
                NakDelay = TimeSpan.FromMilliseconds(nakDelayMilliseconds)
            });

        AssertFailure(result, expectedFailure);
    }

    private static void AssertFailure(ValidateOptionsResult result, string expectedFailure)
    {
        Assert.True(result.Failed);
        Assert.Contains(expectedFailure, result.FailureMessage, StringComparison.Ordinal);
    }

    private static Microsoft.Extensions.Configuration.ConfigurationManager CreateConfiguration(
        params (string Key, string Value)[] values)
    {
        Microsoft.Extensions.Configuration.ConfigurationManager configuration = new();

        foreach ((string key, string value) in values)
        {
            configuration[key] = value;
        }

        return configuration;
    }
}
