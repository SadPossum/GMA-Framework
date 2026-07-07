namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IntegrationEventSubscriptionTests
{
    [Fact]
    public void Registers_subscription_descriptor_for_handler()
    {
        ServiceCollection services = new();

        services.AddIntegrationEventHandler<TestIntegrationEvent, TestIntegrationEventHandler>(
            "ordering",
            "gma.catalog.item-created.v1",
            "catalog-item-created-projection");

        using ServiceProvider provider = services.BuildServiceProvider();
        IIntegrationEventSubscriptionRegistry registry =
            provider.GetRequiredService<IIntegrationEventSubscriptionRegistry>();

        IntegrationEventSubscription subscription = Assert.Single(registry.Subscriptions);
        Assert.Equal("ordering", subscription.ConsumerModule);
        Assert.Equal("catalog-item-created-projection", subscription.HandlerName);
        Assert.Equal(typeof(TestIntegrationEvent), subscription.EventType);
        Assert.Equal(typeof(TestIntegrationEventHandler), subscription.HandlerType);
    }

    [Fact]
    public void Registers_subscription_descriptor_from_event_and_handler_attributes()
    {
        ServiceCollection services = new();

        services.AddIntegrationEventHandler<AttributedIntegrationEvent, AttributedIntegrationEventHandler>(
            "ordering",
            "catalog");

        using ServiceProvider provider = services.BuildServiceProvider();
        IIntegrationEventSubscriptionRegistry registry =
            provider.GetRequiredService<IIntegrationEventSubscriptionRegistry>();

        IntegrationEventSubscription subscription = Assert.Single(registry.Subscriptions);
        Assert.Equal("ordering", subscription.ConsumerModule);
        Assert.Equal("catalog-item-created-projection", subscription.HandlerName);
        Assert.Equal("catalog", subscription.ProducerModule);
        Assert.Equal("item-created", subscription.EventName);
        Assert.Equal(1, subscription.Version);
        Assert.Equal("gma.catalog.item-created.v1", subscription.Subject);
        Assert.True(subscription.IsTenantScoped());
    }

    [Fact]
    public void Attribute_registration_requires_event_metadata()
    {
        ServiceCollection services = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddIntegrationEventHandler<TestIntegrationEvent, AttributedTestIntegrationEventHandler>(
                "ordering",
                "catalog"));

        Assert.Contains(nameof(IntegrationEventNameAttribute), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Attribute_registration_requires_event_version_metadata()
    {
        ServiceCollection services = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddIntegrationEventHandler<MissingVersionIntegrationEvent, MissingVersionIntegrationEventHandler>(
                "ordering",
                "catalog"));

        Assert.Contains(nameof(IntegrationEventVersionAttribute), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Integration_event_version_attribute_rejects_non_positive_versions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntegrationEventVersionAttribute(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntegrationEventVersionAttribute(-1));
    }

    [Fact]
    public void Attribute_registration_requires_handler_metadata()
    {
        ServiceCollection services = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddIntegrationEventHandler<AttributedIntegrationEvent, UnattributedAttributedIntegrationEventHandler>(
                "ordering",
                "catalog"));

        Assert.Contains(nameof(IntegrationEventHandlerAttribute), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_same_handler_registration_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddIntegrationEventHandler<TestIntegrationEvent, TestIntegrationEventHandler>(
            "ordering",
            "gma.catalog.item-created.v1",
            "catalog-item-created-projection");
        services.AddIntegrationEventHandler<TestIntegrationEvent, TestIntegrationEventHandler>(
            "ordering",
            "gma.catalog.item-created.v1",
            "catalog-item-created-projection");

        using ServiceProvider provider = services.BuildServiceProvider();
        IIntegrationEventSubscriptionRegistry registry =
            provider.GetRequiredService<IIntegrationEventSubscriptionRegistry>();

        Assert.Single(registry.Subscriptions);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TestIntegrationEventHandler));
    }

    [Fact]
    public void Repeated_handler_identity_with_different_metadata_fails_registration()
    {
        ServiceCollection services = new();

        services.AddIntegrationEventHandler<TestIntegrationEvent, TestIntegrationEventHandler>(
            "ordering",
            "gma.catalog.item-created.v1",
            "catalog-item-created-projection");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddIntegrationEventHandler<OtherIntegrationEvent, OtherIntegrationEventHandler>(
                "ordering",
                "gma.catalog.item-updated.v1",
                "catalog-item-created-projection"));

        Assert.Contains("already registered with different metadata", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_normalizes_descriptor_values()
    {
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, TestIntegrationEventHandler>(
                " Ordering ",
                " GMA.Catalog.Item-Created.V1 ",
                " Catalog-Item-Created-Projection ");

        Assert.Equal("ordering", subscription.ConsumerModule);
        Assert.Equal("gma.catalog.item-created.v1", subscription.Subject);
        Assert.Equal("catalog-item-created-projection", subscription.HandlerName);
    }

    [Fact]
    public void Factory_keeps_logical_event_identity_separate_from_physical_subject_prefix()
    {
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, TestIntegrationEventHandler>(
                "ordering",
                "gma.catalog.item-created.v1",
                "catalog-item-created-projection");

        Assert.Equal("catalog", subscription.ProducerModule);
        Assert.Equal("item-created", subscription.EventName);
        Assert.Equal(1, subscription.Version);
        Assert.Equal("gma.catalog.item-created.v1", subscription.Subject);
        Assert.Equal("acme-orders.catalog.item-created.v1", subscription.CreateSubject("acme-orders"));
    }

    [Theory]
    [InlineData("ordering.module", "gma.catalog.item-created.v1", "catalog-item-created-projection")]
    [InlineData("ordering", "gma.catalog.item-created", "catalog-item-created-projection")]
    [InlineData("ordering", "gma.catalog.item-created.v01", "catalog-item-created-projection")]
    [InlineData("ordering", "gma.catalog.item-created.v1", "catalog.item-created-projection")]
    [InlineData("ordering", "gma.catalog.item-created.v1", "catalog item created projection")]
    public void Factory_rejects_invalid_descriptor_values(
        string consumerModule,
        string subject,
        string handlerName)
    {
        Assert.Throws<ArgumentException>(() =>
            IntegrationEventSubscription.Create<TestIntegrationEvent, TestIntegrationEventHandler>(
                consumerModule,
                subject,
                handlerName));
    }

    [Fact]
    public void Duplicate_handler_descriptors_fail_registry_creation()
    {
        IntegrationEventSubscription subscription =
            IntegrationEventSubscription.Create<TestIntegrationEvent, TestIntegrationEventHandler>(
                "ordering",
                "gma.catalog.item-created.v1",
                "catalog-item-created-projection");

        Assert.Throws<InvalidOperationException>(() =>
            new IntegrationEventSubscriptionRegistry([subscription, subscription]));
    }

    [Fact]
    public void Registry_rejects_null_subscription_collection()
    {
        Assert.Throws<ArgumentNullException>(() => new IntegrationEventSubscriptionRegistry(null!));
    }

    [Fact]
    public void Registry_rejects_null_subscription_entries()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new IntegrationEventSubscriptionRegistry([null!]));

        Assert.Contains("subscription at index 0 is null", exception.Message, StringComparison.Ordinal);
    }

    private sealed record TestIntegrationEvent(
        Guid EventId,
        string Payload,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent
    {
        public string EventName => "test";
        public int Version => 1;
    }

    private sealed class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [IntegrationEventHandler("catalog-item-created-projection")]
    private sealed class AttributedTestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [IntegrationEventName(EventType)]
    [IntegrationEventVersion(EventVersion)]
    [TenantScoped]
    private sealed record AttributedIntegrationEvent(
        Guid EventId,
        string Payload,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent
    {
        public const string EventType = "item-created";
        public const int EventVersion = 1;

        public string EventName => EventType;
        public int Version => EventVersion;
    }

    [IntegrationEventHandler("catalog-item-created-projection")]
    private sealed class AttributedIntegrationEventHandler : IIntegrationEventHandler<AttributedIntegrationEvent>
    {
        public Task HandleAsync(AttributedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UnattributedAttributedIntegrationEventHandler : IIntegrationEventHandler<AttributedIntegrationEvent>
    {
        public Task HandleAsync(AttributedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    [IntegrationEventName(EventType)]
    private sealed record MissingVersionIntegrationEvent(
        Guid EventId,
        string Payload,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent
    {
        public const string EventType = "missing-version";

        public string EventName => EventType;
        public int Version => 1;
    }

    [IntegrationEventHandler("missing-version-projection")]
    private sealed class MissingVersionIntegrationEventHandler : IIntegrationEventHandler<MissingVersionIntegrationEvent>
    {
        public Task HandleAsync(MissingVersionIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed record OtherIntegrationEvent(
        Guid EventId,
        string Payload,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent
    {
        public string EventName => "other";
        public int Version => 1;
    }

    private sealed class OtherIntegrationEventHandler : IIntegrationEventHandler<OtherIntegrationEvent>
    {
        public Task HandleAsync(OtherIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
