namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Messaging;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantMessagingTests
{
    [Fact]
    public void Tenant_integration_event_owns_tenant_metadata_outside_base_messaging_contract()
    {
        TestTenantIntegrationEvent integrationEvent = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            " tenant-a ",
            new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("tenant-a", integrationEvent.TenantId);
        Assert.Equal("test-event", integrationEvent.EventName);
        Assert.True(integrationEvent is IIntegrationEvent);
        Assert.True(integrationEvent is ITenantIntegrationEvent);
    }

    [Fact]
    public void Tenant_aware_messaging_registers_scope_resolver_and_context_contributor()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Tenancy:Enabled"] = "true";

        builder.AddTenancyInfrastructure();
        builder.AddTenantAwareMessaging();
        ModuleCompositionValidationResult validation = builder.ValidateModuleComposition();

        using IHost host = builder.Build();
        using IServiceScope scope = host.Services.CreateScope();

        Assert.True(validation.IsValid);
        Assert.Single(scope.ServiceProvider.GetServices<IIntegrationEventScopeResolver>());
        Assert.Single(scope.ServiceProvider.GetServices<IIntegrationEventProcessingContextContributor>());
    }

    [Fact]
    public void Tenant_context_contributor_sets_tenant_for_tenant_scoped_subscription()
    {
        using IHost host = CreateTenantAwareMessagingHost();
        using IServiceScope scope = host.Services.CreateScope();
        IIntegrationEventProcessingContextContributor contributor = scope.ServiceProvider
            .GetRequiredService<IIntegrationEventProcessingContextContributor>();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        IntegrationEventSubscription subscription = IntegrationEventSubscription.Create<TestTenantIntegrationEvent, TestTenantEventHandler>(
            "ordering",
            "gma.catalog.test-event.v1",
            "tenant-handler",
            [TenantScopeMetadataItem.Instance]);

        contributor.Prepare(
            subscription,
            new TestTenantIntegrationEvent(
                Guid.NewGuid(),
                "tenant-a",
                DateTimeOffset.UtcNow));

        Assert.Equal("tenant-a", tenantContext.TenantId);
    }

    [Fact]
    public void Tenant_context_contributor_rejects_non_tenant_event_for_tenant_scoped_subscription()
    {
        using IHost host = CreateTenantAwareMessagingHost();
        using IServiceScope scope = host.Services.CreateScope();
        IIntegrationEventProcessingContextContributor contributor = scope.ServiceProvider
            .GetRequiredService<IIntegrationEventProcessingContextContributor>();
        IntegrationEventSubscription subscription = IntegrationEventSubscription.Create<TestPlainIntegrationEvent, TestPlainEventHandler>(
            "ordering",
            "gma.catalog.test-event.v1",
            "tenant-handler",
            [TenantScopeMetadataItem.Instance]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            contributor.Prepare(subscription, new TestPlainIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow)));

        Assert.Contains(nameof(ITenantIntegrationEvent), exception.Message, StringComparison.Ordinal);
    }

    private static IHost CreateTenantAwareMessagingHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Tenancy:Enabled"] = "true";
        builder.AddTenancyInfrastructure();
        builder.AddTenantAwareMessaging();
        builder.ValidateModuleComposition();
        return builder.Build();
    }

    private sealed record TestTenantIntegrationEvent : TenantIntegrationEvent
    {
        public TestTenantIntegrationEvent(Guid eventId, string tenantId, DateTimeOffset occurredAtUtc)
            : base(eventId, tenantId, occurredAtUtc, "test-event", 1)
        {
        }
    }

    private sealed record TestPlainIntegrationEvent : IntegrationEvent
    {
        public TestPlainIntegrationEvent(Guid eventId, DateTimeOffset occurredAtUtc)
            : base(eventId, occurredAtUtc, "test-event", 1)
        {
        }
    }

    private sealed class TestTenantEventHandler : IIntegrationEventHandler<TestTenantIntegrationEvent>
    {
        public Task HandleAsync(TestTenantIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestPlainEventHandler : IIntegrationEventHandler<TestPlainIntegrationEvent>
    {
        public Task HandleAsync(TestPlainIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
