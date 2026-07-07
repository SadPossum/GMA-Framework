namespace Gma.Framework.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EfOutboxWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_normalizes_module_name_through_integration_contracts()
    {
        using TestDbContext dbContext = CreateDbContext();

        TestOutboxWriter writer = new(dbContext, " Catalog ");

        Assert.Equal("catalog", writer.ModuleName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("catalog.module")]
    [InlineData("Catalog Module")]
    public void Constructor_rejects_invalid_module_name(string moduleName)
    {
        using TestDbContext dbContext = CreateDbContext();

        Assert.Throws<ArgumentException>(() => new TestOutboxWriter(dbContext, moduleName));
    }

    [Fact]
    public async Task Enqueue_tracks_outbox_message_without_saving()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxWriter writer = new(dbContext, "catalog");
        TestIntegrationEvent integrationEvent = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "item-created",
            1,
            " tenant-a ",
            new DateTimeOffset(2026, 7, 2, 11, 30, 0, TimeSpan.Zero));

        await writer.EnqueueAsync(integrationEvent, CancellationToken.None);

        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<OutboxMessage> entry =
            Assert.Single(dbContext.ChangeTracker.Entries<OutboxMessage>());
        Assert.Equal(EntityState.Added, entry.State);
        OutboxMessage message = entry.Entity;
        Assert.Equal(integrationEvent.EventId, message.Id);
        Assert.Equal("gma.catalog.item-created.v1", message.Subject);
        Assert.Equal(typeof(TestIntegrationEvent).FullName, message.EventType);
        Assert.Equal(1, message.Version);
        Assert.Null(message.ScopeId);
        Assert.Equal(integrationEvent.OccurredAtUtc, message.OccurredAtUtc);
        Assert.Equal(Now, message.CreatedAtUtc);
        Assert.Contains("\"eventName\":\"item-created\"", message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enqueue_applies_optional_scope_resolver()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxWriter writer = new(
            dbContext,
            "catalog",
            scopeResolvers: [new FixedScopeResolver("tenant-a")]);

        await writer.EnqueueAsync(
            new TestIntegrationEvent(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                "item-created",
                1,
                "tenant-a",
                Now),
            CancellationToken.None);

        OutboxMessage message = Assert.Single(dbContext.ChangeTracker.Entries<OutboxMessage>()).Entity;
        Assert.Equal("tenant-a", message.ScopeId);
    }

    [Fact]
    public async Task Enqueue_uses_configured_application_namespace_for_subject()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxWriter writer = new(dbContext, "catalog", "acme-orders");

        await writer.EnqueueAsync(
            new TestIntegrationEvent(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                "item-created",
                1,
                "tenant-a",
                Now),
            CancellationToken.None);

        OutboxMessage message = Assert.Single(dbContext.ChangeTracker.Entries<OutboxMessage>()).Entity;
        Assert.Equal("acme-orders.catalog.item-created.v1", message.Subject);
    }

    [Fact]
    public async Task Enqueue_rejects_null_event()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxWriter writer = new(dbContext, "catalog");

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            writer.EnqueueAsync<TestIntegrationEvent>(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Enqueue_honors_cancellation_before_tracking_message()
    {
        using TestDbContext dbContext = CreateDbContext();
        TestOutboxWriter writer = new(dbContext, "catalog");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.EnqueueAsync(
                new TestIntegrationEvent(
                    Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    "item-created",
                    1,
                    "tenant-a",
                    Now),
                cancellation.Token));

        Assert.Empty(dbContext.ChangeTracker.Entries<OutboxMessage>());
    }

    private static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestDbContext(options);
    }

    private sealed class TestOutboxWriter(
        TestDbContext dbContext,
        string moduleName,
        string applicationNamespace = ApplicationNamespaces.Default,
        IEnumerable<IIntegrationEventScopeResolver>? scopeResolvers = null)
        : EfOutboxWriter<TestDbContext>(
            dbContext,
            new TestClock(),
            Options.Create(new ApplicationIdentityOptions { Namespace = applicationNamespace }),
            moduleName,
            scopeResolvers);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed record TestIntegrationEvent(
        Guid EventId,
        string EventName,
        int Version,
        string Payload,
        DateTimeOffset OccurredAtUtc) : IIntegrationEvent;

    private sealed class FixedScopeResolver(string scopeId) : IIntegrationEventScopeResolver
    {
        public string? ResolveScopeId(IIntegrationEvent integrationEvent) => scopeId;
    }
}
