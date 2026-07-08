namespace Gma.Framework.Tests;

using System.Reflection;
using Gma.Framework.Authorization;
using Gma.Framework.Caching;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Tasks;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ModuleDescriptorTests
{
    [Fact]
    public void Module_metadata_descriptors_are_constructor_only()
    {
        Type[] descriptorTypes =
        [
            typeof(ModuleDescriptor),
            typeof(ModuleDescriptorFeature),
            typeof(ModuleDescriptorFeatureContext),
            typeof(ModulePermissionsDescriptor),
            typeof(ModulePermissionDescriptor),
            typeof(ModulePublishedEventsDescriptor),
            typeof(ModuleIntegrationEventDescriptor),
            typeof(ModuleSubscriptionsDescriptor),
            typeof(ModuleSubscriptionDescriptor),
            typeof(ModuleCacheEntriesDescriptor),
            typeof(ModuleCacheDescriptor),
            typeof(ModuleTasksDescriptor),
            typeof(ModuleTaskDescriptor),
            typeof(ModuleCompositionDescriptor),
            typeof(ModuleProfileDescriptor),
            typeof(ProvidedCompositionFeature),
            typeof(RequiredCompositionFeature),
            typeof(RequiredCompositionModule)
        ];

        string[] writableProperties = descriptorTypes
            .SelectMany(type => type
                .GetProperties()
                .Where(property => property.SetMethod is not null)
                .Select(property => $"{type.Name}.{property.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(writableProperties);
    }

    [Fact]
    public void Module_descriptor_is_authored_through_builder_only()
    {
        ConstructorInfo[] publicConstructors = typeof(ModuleDescriptor).GetConstructors();

        Assert.Empty(publicConstructors);
    }

    [Fact]
    public void Module_descriptor_root_is_sealed_while_features_are_polymorphic()
    {
        Assert.True(typeof(ModuleDescriptor).IsSealed);
        Assert.True(typeof(ModuleDescriptorFeature).IsAbstract);
        Assert.True(typeof(ModulePermissionsDescriptor).IsSealed);
        Assert.True(typeof(ModulePublishedEventsDescriptor).IsSealed);
        Assert.True(typeof(ModuleSubscriptionsDescriptor).IsSealed);
        Assert.True(typeof(ModuleCacheEntriesDescriptor).IsSealed);
        Assert.True(typeof(ModuleTasksDescriptor).IsSealed);
        Assert.True(typeof(ModuleCompositionDescriptor).IsSealed);
        Assert.True(typeof(ModulePermissionsDescriptor).IsSubclassOf(typeof(ModuleDescriptorFeature)));
        Assert.True(typeof(ModulePublishedEventsDescriptor).IsSubclassOf(typeof(ModuleDescriptorFeature)));
        Assert.True(typeof(ModuleSubscriptionsDescriptor).IsSubclassOf(typeof(ModuleDescriptorFeature)));
        Assert.True(typeof(ModuleCacheEntriesDescriptor).IsSubclassOf(typeof(ModuleDescriptorFeature)));
        Assert.True(typeof(ModuleTasksDescriptor).IsSubclassOf(typeof(ModuleDescriptorFeature)));
        Assert.True(typeof(ModuleCompositionDescriptor).IsSubclassOf(typeof(ModuleDescriptorFeature)));
    }

    [Fact]
    public void Module_metadata_descriptor_constructor_parameters_use_camel_case()
    {
        Type[] descriptorTypes =
        [
            typeof(ModuleDescriptor),
            typeof(ModuleDescriptorFeatureContext),
            typeof(ModulePermissionsDescriptor),
            typeof(ModulePermissionDescriptor),
            typeof(ModulePublishedEventsDescriptor),
            typeof(ModuleIntegrationEventDescriptor),
            typeof(ModuleSubscriptionsDescriptor),
            typeof(ModuleSubscriptionDescriptor),
            typeof(ModuleCacheEntriesDescriptor),
            typeof(ModuleCacheDescriptor),
            typeof(ModuleTasksDescriptor),
            typeof(ModuleTaskDescriptor),
            typeof(ModuleCompositionDescriptor),
            typeof(ModuleProfileDescriptor),
            typeof(ProvidedCompositionFeature),
            typeof(RequiredCompositionFeature),
            typeof(RequiredCompositionModule)
        ];

        string[] offenders = descriptorTypes
            .SelectMany(type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor
                    .GetParameters()
                    .Where(parameter => parameter.Name is { Length: > 0 } name &&
                                        char.IsUpper(name[0]))
                    .Select(parameter => $"{type.Name}.{parameter.Name}")))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_metadata_helpers_keep_naming_and_guard_responsibilities_separate()
    {
        string[] namingMethods = typeof(ModuleMetadataNaming)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] guardMethods = typeof(ModuleMetadataGuards)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["NormalizeFeatureKey", "NormalizeModuleName"], namingMethods);
        Assert.Equal(["CopyOptionalList", "CopyRequiredList", "CopyRequiredNonEmptyList", "EnsureUnique"], guardMethods);
    }

    [Fact]
    public void Module_descriptor_builder_normalizes_public_metadata()
    {
        ModuleDescriptor descriptor = ModuleDescriptor
            .Create(" Catalog ")
            .WithSchema(" Catalog ")
            .WithAdminSurfaceName(" Catalog-Admin ")
            .WithPermissions([
                new ModulePermissionDescriptor(" Catalog.Items.Read ", " Read catalog items. ", tenantScoped: true)
            ])
            .WithPublishedEvents([
                new ModuleIntegrationEventDescriptor(" Item-Created ", " GMA.Catalog.Item-Created.V1 ", 1, [TenantScopeMetadataItem.Instance])
            ])
            .WithSubscriptions([
                new ModuleSubscriptionDescriptor(
                    " Catalog ",
                    " Item-Created ",
                    " GMA.Catalog.Item-Created.V1 ",
                    " Item-Created-Projection ",
                    [TenantScopeMetadataItem.Instance])
            ])
            .WithCacheEntries([
                new ModuleCacheDescriptor(" Items ", CacheScope.Tenant, [" Products "])
            ])
            .WithTasks([
                new ModuleTaskDescriptor(
                    " Rebuild-Search ",
                    " Rebuild search index. ",
                    ModuleTaskKind.OneShot,
                    supportsControlMessages: true,
                    " Search-Workers ",
                    metadata: [TenantScopeMetadataItem.Instance])
            ])
            .WithProfile(new ModuleProfileDescriptor(
                " Catalog ",
                " Tenant-Scoped ",
                provides: [new ProvidedCompositionFeature(new CompositionFeatureId(" Catalog.Items "), " catalog/tenant-scoped ")]))
            .Build();

        Assert.Equal("catalog", descriptor.Name);
        Assert.Equal("catalog", descriptor.Schema);
        Assert.Equal("catalog-admin", descriptor.AdminSurfaceName);
        Assert.Equal(6, descriptor.Features.Count);
        Assert.Equal("catalog.items.read", Assert.Single(descriptor.GetPermissions()).Code);
        ModuleIntegrationEventDescriptor publishedEvent = Assert.Single(descriptor.GetPublishedEvents());
        Assert.Equal("catalog", publishedEvent.ModuleName);
        Assert.Equal("item-created", publishedEvent.EventType);
        Assert.Equal("item-created-projection", Assert.Single(descriptor.GetSubscriptions()).HandlerName);
        Assert.Equal("items", Assert.Single(descriptor.GetCacheEntries()).Name);
        Assert.Equal(CacheScope.Tenant, Assert.Single(descriptor.GetCacheEntries()).Scope);
        Assert.Equal(["products"], Assert.Single(descriptor.GetCacheEntries()).Tags);
        Assert.Equal("rebuild-search", Assert.Single(descriptor.GetTasks()).Name);
        Assert.Equal("search-workers", Assert.Single(descriptor.GetTasks()).WorkerGroup);
        Assert.Equal("tenant-scoped", Assert.Single(descriptor.GetCompositionProfiles()).ProfileName);
    }

    [Fact]
    public void Module_descriptor_capability_metadata_can_be_authored_incrementally()
    {
        ModuleDescriptor descriptor = ModuleDescriptor
            .Create("catalog")
            .WithPermission(new ModulePermissionDescriptor("catalog.items.read", "Read catalog items.", tenantScoped: true))
            .WithPermissions([
                new ModulePermissionDescriptor("catalog.items.create", "Create catalog items.", tenantScoped: true)
            ])
            .WithPublishedEvent(new ModuleIntegrationEventDescriptor("item-created", "gma.catalog.item-created.v1", 1, [TenantScopeMetadataItem.Instance]))
            .WithPublishedEvents([
                new ModuleIntegrationEventDescriptor("item-updated", "gma.catalog.item-updated.v1", 1, [TenantScopeMetadataItem.Instance])
            ])
            .WithSubscription(new ModuleSubscriptionDescriptor(
                "catalog",
                "item-created",
                "gma.catalog.item-created.v1",
                "item-created-projection",
                [TenantScopeMetadataItem.Instance]))
            .WithSubscriptions([
                new ModuleSubscriptionDescriptor(
                    "catalog",
                    "item-updated",
                    "gma.catalog.item-updated.v1",
                    "item-updated-projection",
                    [TenantScopeMetadataItem.Instance])
            ])
            .WithCacheEntry(new ModuleCacheDescriptor("item", CacheScope.Tenant, ["catalog.items"]))
            .WithCacheEntries([
                new ModuleCacheDescriptor("items", CacheScope.Tenant, ["catalog.items"])
            ])
            .WithTask(new ModuleTaskDescriptor(
                "rebuild-item",
                "Rebuild one catalog item projection.",
                ModuleTaskKind.OneShot,
                supportsControlMessages: false,
                "catalog-workers",
                metadata: [TenantScopeMetadataItem.Instance]))
            .WithTasks([
                new ModuleTaskDescriptor(
                    "rebuild-items",
                    "Rebuild catalog item projections.",
                    ModuleTaskKind.OneShot,
                    supportsControlMessages: true,
                    "catalog-workers",
                    metadata: [TenantScopeMetadataItem.Instance])
            ])
            .WithProfile(new ModuleProfileDescriptor("catalog", "default"))
            .WithProfiles([new ModuleProfileDescriptor("catalog", "durable-events")])
            .Build();

        Assert.Equal(6, descriptor.Features.Count);
        Assert.Equal(["catalog.items.read", "catalog.items.create"], descriptor.GetPermissions().Select(permission => permission.Code));
        Assert.Equal(["item-created", "item-updated"], descriptor.GetPublishedEvents().Select(item => item.EventType));
        Assert.Equal(["item-created-projection", "item-updated-projection"], descriptor.GetSubscriptions().Select(item => item.HandlerName));
        Assert.Equal(["item", "items"], descriptor.GetCacheEntries().Select(item => item.Name));
        Assert.Equal(["rebuild-item", "rebuild-items"], descriptor.GetTasks().Select(item => item.Name));
        Assert.Equal(["default", "durable-events"], descriptor.GetCompositionProfiles().Select(item => item.ProfileName));
    }

    [Fact]
    public void Module_descriptor_can_read_local_metadata_from_event_and_task_attributes()
    {
        ModuleDescriptor descriptor = ModuleDescriptor
            .Create("catalog")
            .WithPublishedEvent<AttributedIntegrationEvent>()
            .WithSubscription<AttributedIntegrationEvent>("catalog", "item-created-projection")
            .WithTask<AttributedTaskPayload>()
            .Build();

        ModuleIntegrationEventDescriptor publishedEvent = Assert.Single(descriptor.GetPublishedEvents());
        Assert.Equal("catalog", publishedEvent.ModuleName);
        Assert.Equal("item-created", publishedEvent.EventType);
        Assert.Equal("gma.catalog.item-created.v1", publishedEvent.Subject);
        Assert.True(publishedEvent.IsTenantScoped());

        ModuleSubscriptionDescriptor subscription = Assert.Single(descriptor.GetSubscriptions());
        Assert.Equal("catalog", subscription.ProducerModule);
        Assert.Equal("item-created", subscription.EventType);
        Assert.Equal("gma.catalog.item-created.v1", subscription.Subject);
        Assert.Equal("item-created-projection", subscription.HandlerName);
        Assert.True(subscription.IsTenantScoped());

        ModuleTaskDescriptor task = Assert.Single(descriptor.GetTasks());
        Assert.Equal("rebuild-search", task.Name);
        Assert.Equal("Rebuild search index.", task.Description);
        Assert.Equal(ModuleTaskKind.OneShot, task.Kind);
        Assert.Equal("search-workers", task.WorkerGroup);
        Assert.Equal(2, task.PayloadVersion);
        Assert.True(task.IsTenantScoped());
        Assert.True(task.SupportsControlMessages);
    }

    [Fact]
    public void Built_in_capability_feature_descriptors_reject_empty_lists()
    {
        Assert.Throws<ArgumentException>(() => new ModulePermissionsDescriptor([]));
        Assert.Throws<ArgumentException>(() => new ModulePublishedEventsDescriptor([]));
        Assert.Throws<ArgumentException>(() => new ModuleSubscriptionsDescriptor([]));
        Assert.Throws<ArgumentException>(() => new ModuleCacheEntriesDescriptor([]));
        Assert.Throws<ArgumentException>(() => new ModuleTasksDescriptor([]));
        Assert.Throws<ArgumentException>(() => new ModuleCompositionDescriptor([]));
    }

    [Fact]
    public void Module_descriptor_bulk_capability_helpers_reject_empty_lists()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor.Create("catalog");

        Assert.Throws<ArgumentException>(() => builder.WithPermissions([]));
        Assert.Throws<ArgumentException>(() => builder.WithPublishedEvents([]));
        Assert.Throws<ArgumentException>(() => builder.WithSubscriptions([]));
        Assert.Throws<ArgumentException>(() => builder.WithCacheEntries([]));
        Assert.Throws<ArgumentException>(() => builder.WithTasks([]));
        Assert.Throws<ArgumentException>(() => builder.WithProfiles([]));
    }

    [Fact]
    public void Module_descriptor_supports_custom_polymorphic_features()
    {
        ModuleDescriptor descriptor = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("custom value"))
            .Build();

        TestFeature feature = Assert.IsType<TestFeature>(Assert.Single(descriptor.Features));
        Assert.Same(feature, descriptor.GetFeature<TestFeature>());
        Assert.Same(feature, Assert.Single(descriptor.GetFeatures<TestFeature>()));
        Assert.Equal("custom value", feature.Value);
    }

    [Fact]
    public void Module_descriptor_build_copies_builder_state()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("initial value"));

        ModuleDescriptor descriptor = builder.Build();

        builder.WithFeature(new OtherFeature());

        TestFeature feature = Assert.IsType<TestFeature>(Assert.Single(descriptor.Features));
        Assert.Equal("initial value", feature.Value);
        Assert.DoesNotContain(descriptor.Features, item => item is OtherFeature);
    }

    [Fact]
    public void Module_descriptor_rejects_null_custom_feature()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor.Create("catalog");

        Assert.Throws<ArgumentNullException>(() => builder.WithFeature(null!));
    }

    [Fact]
    public void Module_descriptor_rejects_null_custom_feature_merge()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor.Create("catalog");

        Assert.Throws<ArgumentNullException>(() => builder.WithFeature(new TestFeature("value"), null!));
    }

    [Fact]
    public void Module_descriptor_typed_merge_rejects_feature_key_changes()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new VariableKeyFeature("test.one"), static (existing, incoming) => incoming);

        Assert.Throws<InvalidOperationException>(() => builder.WithFeature(
            new VariableKeyFeature("test.one"),
            static (_, _) => new VariableKeyFeature("test.two")));
    }

    [Fact]
    public void Module_descriptor_typed_merge_rejects_same_key_different_feature_type()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("value"));

        Assert.Throws<InvalidOperationException>(() => builder.WithFeature(
            new KeyCollisionFeature(),
            static (_, incoming) => incoming));
    }

    [Fact]
    public void Module_descriptor_typed_merge_rejects_base_typed_same_key_different_feature_type()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("value"));

        Assert.Throws<InvalidOperationException>(() => builder.WithFeature<ModuleDescriptorFeature>(
            new KeyCollisionFeature(),
            static (_, incoming) => incoming));
    }

    [Fact]
    public void Module_descriptor_typed_merge_rejects_base_typed_result_type_changes()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("value"));

        Assert.Throws<InvalidOperationException>(() => builder.WithFeature<ModuleDescriptorFeature>(
            new TestFeature("incoming"),
            static (_, _) => new KeyCollisionFeature()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-catalog")]
    [InlineData("catalog_legacy")]
    public void Module_descriptor_create_rejects_invalid_module_name(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => ModuleDescriptor.Create(name!));
    }

    [Fact]
    public void Module_descriptor_empty_uses_builder_normalization()
    {
        ModuleDescriptor descriptor = ModuleDescriptor.Empty(" Catalog ", " Catalog ", " Catalog-Admin ");

        Assert.Equal("catalog", descriptor.Name);
        Assert.Equal("catalog", descriptor.Schema);
        Assert.Equal("catalog-admin", descriptor.AdminSurfaceName);
        Assert.Empty(descriptor.Features);
    }

    [Fact]
    public void Module_descriptor_get_feature_requires_unique_matching_type()
    {
        ModuleDescriptor descriptor = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new VariableKeyFeature("feature.one"))
            .WithFeature(new VariableKeyFeature("feature.two"))
            .Build();

        Assert.Equal(2, descriptor.GetFeatures<VariableKeyFeature>().Count);
        Assert.Throws<InvalidOperationException>(() => descriptor.GetFeature<VariableKeyFeature>());
    }

    [Theory]
    [InlineData("feature")]
    [InlineData("feature.")]
    [InlineData(".feature")]
    [InlineData("feature..one")]
    public void Module_descriptor_feature_keys_must_be_namespaced(string key)
    {
        Assert.Throws<ArgumentException>(() => new VariableKeyFeature(key));
    }

    [Fact]
    public void Module_descriptor_runs_custom_feature_validation()
    {
        Assert.Throws<InvalidOperationException>(() => ModuleDescriptor
            .Create("auth")
            .WithFeature(new CatalogOnlyFeature())
            .Build());
    }

    [Fact]
    public void Module_descriptor_rejects_duplicate_features()
    {
        Assert.Throws<ArgumentException>(() => ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("one"))
            .WithFeature(new TestFeature("two")));
    }

    [Fact]
    public void Module_descriptor_raw_feature_authoring_rejects_duplicate_keys_immediately()
    {
        ModuleDescriptorBuilder builder = ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("one"));

        Assert.Throws<ArgumentException>(() => builder.WithFeature(new KeyCollisionFeature()));

        ModuleDescriptor descriptor = builder.Build();
        TestFeature feature = Assert.IsType<TestFeature>(Assert.Single(descriptor.Features));
        Assert.Equal("one", feature.Value);
    }

    [Fact]
    public void Module_descriptor_rejects_duplicate_feature_keys_before_feature_validation()
    {
        Assert.Throws<ArgumentException>(() => ModuleDescriptor
            .Create("catalog")
            .WithFeature(new TestFeature("one"))
            .WithFeature(new ThrowingKeyCollisionFeature()));
    }

    [Fact]
    public void Module_descriptor_rejects_duplicate_public_metadata()
    {
        Assert.Throws<ArgumentException>(() => ModuleDescriptor
            .Create("catalog")
            .WithSchema("catalog")
            .WithPermission(new ModulePermissionDescriptor("catalog.items.read", "Read catalog items.", tenantScoped: true))
            .WithPermission(new ModulePermissionDescriptor("Catalog.Items.Read", "Read catalog items.", tenantScoped: true)));
    }

    [Fact]
    public void Module_descriptor_rejects_duplicate_task_metadata()
    {
        Assert.Throws<ArgumentException>(() => ModuleDescriptor
            .Create("catalog")
            .WithSchema("catalog")
            .WithTask(new ModuleTaskDescriptor("rebuild-search", "Rebuild search index.", ModuleTaskKind.OneShot, supportsControlMessages: true))
            .WithTask(new ModuleTaskDescriptor("Rebuild-Search", "Rebuild search index.", ModuleTaskKind.Daemon, supportsControlMessages: true)));
    }

    [Fact]
    public void Module_descriptor_rejects_published_event_subject_for_another_module()
    {
        Assert.Throws<ArgumentException>(() => ModuleDescriptor
            .Create("catalog")
            .WithSchema("catalog")
            .WithPublishedEvents([
                new ModuleIntegrationEventDescriptor("item-created", "gma.auth.item-created.v1", 1)
            ])
            .Build());
    }

    [Fact]
    public void Module_subscription_rejects_subject_that_does_not_match_producer_event()
    {
        Assert.Throws<ArgumentException>(() => new ModuleSubscriptionDescriptor(
            "catalog",
            "item-created",
            "gma.catalog.item-updated.v1",
            "item-created-projection"));
    }

    [Fact]
    public void Module_cache_metadata_derives_tenant_scope_from_scope()
    {
        ModuleCacheDescriptor tenantScoped = new(
            "items",
            CacheScope.Tenant,
            ["items"]);
        ModuleCacheDescriptor global = new(
            "items",
            CacheScope.Global,
            ["items"]);

        Assert.True(tenantScoped.TenantScoped);
        Assert.False(global.TenantScoped);
    }

    [Fact]
    public void Module_cache_metadata_rejects_unknown_scope()
    {
        Assert.Throws<ArgumentException>(() => new ModuleCacheDescriptor(
            "items",
            CacheScope.Unknown,
            ["items"]));
    }

    [Fact]
    public void Module_task_metadata_rejects_unknown_kind()
    {
        Assert.Throws<ArgumentException>(() => new ModuleTaskDescriptor(
            "rebuild-search",
            "Rebuild search index.",
            ModuleTaskKind.Unknown,
            supportsControlMessages: true));
    }

    [Theory]
    [InlineData("catalog")]
    [InlineData("catalog..items.read")]
    [InlineData("catalog_items.read")]
    public void Module_permission_descriptor_rejects_invalid_permission_codes(string code)
    {
        Assert.Throws<ArgumentException>(() =>
            new ModulePermissionDescriptor(code, "Read catalog items.", tenantScoped: true));
    }

    private sealed record TestFeature : ModuleDescriptorFeature
    {
        public TestFeature(string value)
            : base("test.feature")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            this.Value = value;
        }

        public string Value { get; }
    }

    private sealed record CatalogOnlyFeature : ModuleDescriptorFeature
    {
        public CatalogOnlyFeature()
            : base("test.catalog-only")
        {
        }

        public override void Validate(ModuleDescriptorFeatureContext context)
        {
            base.Validate(context);
            if (!string.Equals(context.ModuleName, "catalog", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("This test feature can only be used by the catalog module.");
            }
        }
    }

    private sealed record OtherFeature : ModuleDescriptorFeature
    {
        public OtherFeature()
            : base("test.other")
        {
        }
    }

    private sealed record KeyCollisionFeature : ModuleDescriptorFeature
    {
        public KeyCollisionFeature()
            : base("test.feature")
        {
        }
    }

    private sealed record ThrowingKeyCollisionFeature : ModuleDescriptorFeature
    {
        public ThrowingKeyCollisionFeature()
            : base("test.feature")
        {
        }

        public override void Validate(ModuleDescriptorFeatureContext context)
        {
            base.Validate(context);
            throw new InvalidOperationException("Feature validation should not run after a duplicate key is found.");
        }
    }

    private sealed record VariableKeyFeature : ModuleDescriptorFeature
    {
        public VariableKeyFeature(string key)
            : base(key)
        {
        }
    }

    [IntegrationEventName(EventType)]
    [IntegrationEventVersion(EventVersion)]
    [TenantScoped]
    private sealed record AttributedIntegrationEvent(
        Guid EventId,
        string TenantId,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent
    {
        public const string EventType = "item-created";
        public const int EventVersion = 1;

        public string EventName => EventType;
        public int Version => EventVersion;
    }

    [TaskName(TaskName)]
    [TaskPayloadVersion(PayloadVersion)]
    [TaskDescription("Rebuild search index.")]
    [TaskKind(ModuleTaskKind.OneShot)]
    [TaskWorkerGroup("search-workers")]
    [SupportsTaskControl]
    [TenantScoped]
    private sealed record AttributedTaskPayload : ITaskPayload
    {
        public const string TaskName = "rebuild-search";
        public const int PayloadVersion = 2;
    }
}
