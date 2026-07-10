namespace Gma.Framework.Tests;

using Microsoft.Extensions.Hosting;
using Gma.Framework.Caching;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Modules;
using Gma.Framework.Notifications;
using Gma.Framework.Notifications.Api;
using Gma.Framework.Notifications.Cqrs;
using Gma.Framework.Notifications.SignalR;
using Gma.Framework.Realtime.Notifications;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using Gma.Framework.Tasks.Infrastructure;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Scoping;
using Gma.Framework.Tenancy.Tasks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ModuleCompositionFeatureTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("feature")]
    [InlineData("feature.")]
    [InlineData(".feature")]
    [InlineData("feature..one")]
    [InlineData("feature_one.enabled")]
    public void Feature_ids_reject_invalid_values(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new CompositionFeatureId(value!));
    }

    [Fact]
    public void Feature_ids_normalize_stable_dotted_kebab_names()
    {
        CompositionFeatureId id = new(" Messaging.Outbox ");

        Assert.Equal("messaging.outbox", id.Value);
        Assert.Equal("messaging.outbox", id.ToString());
    }

    [Fact]
    public void Missing_required_feature_fails_with_clear_error()
    {
        ModuleProfileDescriptor profile = new(
            "ordering",
            "live-projections",
            requires:
            [
                new RequiredCompositionFeature(
                    new CompositionFeatureId("messaging.nats-consumers"),
                    "ordering/live-projections",
                    reason: "Enable the NATS consumer adapter.")
            ]);

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            selectedProfiles: [new SelectedModuleProfile(profile)]));

        Assert.False(result.IsValid);
        string error = Assert.Single(result.Errors);
        Assert.Contains("ordering/live-projections", error, StringComparison.Ordinal);
        Assert.Contains("messaging.nats-consumers", error, StringComparison.Ordinal);
        Assert.Contains("Enable the NATS consumer adapter.", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Optional_missing_feature_is_reported_but_does_not_fail()
    {
        ModuleProfileDescriptor profile = new(
            "catalog",
            "default",
            requires:
            [
                new RequiredCompositionFeature(
                    new CompositionFeatureId("caching.application"),
                    "catalog/default",
                    optional: true,
                    reason: "Catalog can fall back to direct reads.")
            ]);

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            selectedProfiles: [new SelectedModuleProfile(profile)]));

        Assert.True(result.IsValid);
        Assert.Contains("optional missing", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_required_module_fails()
    {
        ModuleProfileDescriptor profile = new(
            "ordering",
            "default",
            requiredModules:
            [
                new RequiredCompositionModule(
                    "catalog",
                    "ordering/default",
                    reason: "Ordering example needs Catalog contracts and projections.")
            ]);

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            selectedProfiles: [new SelectedModuleProfile(profile)]));

        Assert.False(result.IsValid);
        Assert.Contains("requires module 'catalog'", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_exclusive_feature_providers_fail_deterministically()
    {
        CompositionFeatureId feature = new("messaging.outbox");

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            providedFeatures:
            [
                new ProvidedCompositionFeature(feature, "adapter-b"),
                new ProvidedCompositionFeature(feature, "adapter-a")
            ]));

        Assert.False(result.IsValid);
        Assert.Equal(
            "Feature 'messaging.outbox' is provided by multiple providers ('adapter-a', 'adapter-b') but is exclusive.",
            Assert.Single(result.Errors));
    }

    [Fact]
    public void Duplicate_multi_provider_features_are_allowed()
    {
        CompositionFeatureId feature = new("tenancy.context");

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            providedFeatures:
            [
                new ProvidedCompositionFeature(feature, "Gma.Framework.Tenancy.Infrastructure", allowMultipleProviders: true),
                new ProvidedCompositionFeature(feature, "tenancy/default", allowMultipleProviders: true)
            ]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Multiple_profiles_for_same_module_fail()
    {
        ModuleProfileDescriptor global = new("auth", "global");
        ModuleProfileDescriptor tenantScoped = new("auth", "scope-aware");

        ModuleCompositionValidationResult result = ModuleCompositionValidator.Validate(new ModuleCompositionSnapshot(
            selectedProfiles: [new SelectedModuleProfile(global), new SelectedModuleProfile(tenantScoped)]));

        Assert.False(result.IsValid);
        Assert.Contains("Module 'auth' has multiple selected profiles", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Descriptor_metadata_exposes_composition_profiles()
    {
        ModuleProfileDescriptor profile = new(
            "catalog",
            "default",
            provides: [new ProvidedCompositionFeature(new CompositionFeatureId("catalog.items"), "catalog/default")]);

        ModuleDescriptor descriptor = ModuleDescriptor
            .Create("catalog")
            .WithProfile(profile)
            .Build();

        ModuleProfileDescriptor actual = Assert.Single(descriptor.GetCompositionProfiles());
        Assert.Equal("catalog", actual.ModuleName);
        Assert.Equal("default", actual.ProfileName);
        Assert.Equal("catalog.items", Assert.Single(actual.Provides).Id.Value);
    }

    [Fact]
    public void Builder_validation_fails_without_starting_host()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.SelectModuleProfile(new ModuleProfileDescriptor(
            "auth",
            "scope-aware",
            requires: [new RequiredCompositionFeature(new CompositionFeatureId("tenancy.context"), "auth/scope-aware")]));

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());

        Assert.Contains("tenancy.context", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Shared_composition_feature_ids_are_stable()
    {
        Assert.Equal("caching.application", CachingCompositionFeatures.Application.Value);
        Assert.Equal("caching.invalidation", CachingCompositionFeatures.Invalidation.Value);
        Assert.Equal("caching.scope-context", CachingCompositionFeatures.ScopeContext.Value);
        Assert.Equal("messaging.outbox", MessagingCompositionFeatures.Outbox.Value);
        Assert.Equal("messaging.event-bus", MessagingCompositionFeatures.EventBus.Value);
        Assert.Equal("notifications.history", NotificationsCompositionFeatures.History.Value);
        Assert.Equal("notifications.signalr", NotificationsCompositionFeatures.SignalR.Value);
        Assert.Equal("tasks.run-store", TasksCompositionFeatures.RunStore.Value);
        Assert.Equal("tasks.scope-context", TasksCompositionFeatures.ScopeContext.Value);
        Assert.Equal("tasks.worker", TasksCompositionFeatures.Worker.Value);
    }

    [Fact]
    public void Shared_adapter_registrations_advertise_composition_features()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Notifications:Enabled"] = "true";
        builder.Configuration["NatsConsumers:Enabled"] = "true";

        builder.AddCachingCqrs();
        builder.AddTenancyInfrastructure();
        builder.AddTenantScoping();
        builder.AddTenantCaching();
        builder.AddTenantTaskExecutionContext();
        builder.AddNatsJetStreamMessaging();
        builder.AddNatsJetStreamConsumers();
        builder.AddUserNotificationsCqrs();
        builder.AddUserNotificationsRealtime();
        builder.AddUserNotificationServerSentEvents();
        builder.AddUserNotificationSignalR();
        builder.ProvideFeature(TasksCompositionFeatures.RunStoreProvided("test/task-store"));
        builder.AddTaskCqrs();
        builder.AddTaskWorkerRuntime();
        builder.AddTaskRunScheduling();

        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, result.Report);
        Assert.Contains("caching.application by Gma.Framework.Caching.Infrastructure", result.Report, StringComparison.Ordinal);
        Assert.Contains("caching.cqrs-invalidation by Gma.Framework.Caching.Cqrs", result.Report, StringComparison.Ordinal);
        Assert.Contains("caching.scope-context by Gma.Framework.Tenancy.Caching", result.Report, StringComparison.Ordinal);
        Assert.Contains("tasks.scope-context by Gma.Framework.Tenancy.Tasks", result.Report, StringComparison.Ordinal);
        Assert.Contains("messaging.nats-publishing by Gma.Framework.Messaging.Nats", result.Report, StringComparison.Ordinal);
        Assert.Contains("messaging.nats-consumers by Gma.Framework.Messaging.Nats", result.Report, StringComparison.Ordinal);
        Assert.Contains("notifications.live-feed by Gma.Framework.Realtime.Notifications", result.Report, StringComparison.Ordinal);
        Assert.Contains("notifications.sse by Gma.Framework.Notifications.Api", result.Report, StringComparison.Ordinal);
        Assert.Contains("notifications.signalr by Gma.Framework.Notifications.SignalR", result.Report, StringComparison.Ordinal);
        Assert.Contains("tasks.worker by Gma.Framework.Tasks.Infrastructure", result.Report, StringComparison.Ordinal);
        Assert.Contains("tasks.scheduler by Gma.Framework.Tasks.Infrastructure", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Tenant_caching_requires_tenant_context_provider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddTenantCaching();

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());

        Assert.Contains("tenancy.context", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ITenantContext", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Outbox_publishing_requires_concrete_event_bus()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddOutboxPublishing();

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());

        Assert.Contains("messaging.event-bus", exception.Message, StringComparison.Ordinal);
        Assert.Contains("concrete messaging adapter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Task_worker_runtime_requires_persisted_run_store()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddTaskWorkerRuntime();

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());

        Assert.Contains("tasks.run-store", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Gma.Modules.TaskRuntime.Persistence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enabled_notification_sse_requires_live_feed_runtime()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Notifications:Enabled"] = "true";
        builder.Configuration["Notifications:Sse:Enabled"] = "true";

        builder.AddUserNotificationServerSentEvents();

        ModuleCompositionValidationException exception = Assert.Throws<ModuleCompositionValidationException>(
            () => builder.ValidateModuleComposition());

        Assert.Contains("notifications.live-feed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("notification SSE streaming", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_notification_sse_does_not_require_live_feed_runtime()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddUserNotificationServerSentEvents();

        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();

        Assert.True(result.IsValid, result.Report);
        Assert.DoesNotContain("notifications.live-feed", result.Report, StringComparison.Ordinal);
    }
}
