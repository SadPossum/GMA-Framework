namespace Gma.Framework.Tests;

using Gma.Framework.Application.Events;
using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EfDomainEventInboxStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Process_async_dispatches_tracked_domain_events_before_committing_the_inbox()
    {
        await using TestDbContext dbContext = CreateDbContext();
        TestAggregate aggregate = new(Guid.NewGuid());
        dbContext.Aggregates.Add(aggregate);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        RecordingDomainEventDispatcher dispatcher = new(dbContext);
        TestInboxStore store = new(dbContext, dispatcher);
        TestAggregate? handledAggregate = null;

        InboxProcessResult result = await store.ProcessAsync(
            CreateMessageRecord(),
            async cancellationToken =>
            {
                TestAggregate tracked = await dbContext.Aggregates.SingleAsync(cancellationToken);
                handledAggregate = tracked;
                tracked.Touch();
            },
            CancellationToken.None);

        Assert.Equal(InboxProcessStatus.Processed, result.Status);
        Assert.Equal(InboxMessageStatus.Processed, (await dbContext.InboxMessages.SingleAsync()).Status);
        Assert.Equal(1, (await dbContext.Aggregates.SingleAsync()).Revision);
        Assert.Single(await dbContext.Projections.ToArrayAsync());
        Assert.Single(dispatcher.DispatchedEvents);
        Assert.Empty(Assert.IsType<TestAggregate>(handledAggregate).DomainEvents);
    }

    [Fact]
    public async Task Process_async_records_failure_without_saving_mutation_when_dispatch_fails()
    {
        await using TestDbContext dbContext = CreateDbContext();
        dbContext.Aggregates.Add(new TestAggregate(Guid.NewGuid()));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        TestInboxStore store = new(dbContext, new ThrowingDomainEventDispatcher());

        InboxProcessResult result = await store.ProcessAsync(
            CreateMessageRecord(),
            async cancellationToken =>
            {
                TestAggregate tracked = await dbContext.Aggregates.SingleAsync(cancellationToken);
                tracked.Touch();
            },
            CancellationToken.None);

        Assert.Equal(InboxProcessStatus.Failed, result.Status);
        Assert.Equal(0, (await dbContext.Aggregates.AsNoTracking().SingleAsync()).Revision);
        Assert.Equal(InboxMessageStatus.Failed, (await dbContext.InboxMessages.SingleAsync()).Status);
        Assert.Empty(dbContext.Projections);
    }

    private static TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContext(options);
    }

    private static InboxMessageRecord CreateMessageRecord() =>
        new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "test-event-handler",
            "gma.test.event.v1",
            "test-event",
            1,
            "tenant-a",
            Now);

    private sealed class TestInboxStore(
        TestDbContext dbContext,
        IDomainEventDispatcher domainEventDispatcher)
        : EfDomainEventInboxStore<TestDbContext>(
            dbContext,
            new TestClock(),
            new TestIdGenerator(),
            domainEventDispatcher,
            "test");

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
        public DbSet<TestAggregate> Aggregates => this.Set<TestAggregate>();
        public DbSet<TestProjection> Projections => this.Set<TestProjection>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InboxMessage>().ConfigureInboxMessage();
            modelBuilder.Entity<TestAggregate>(builder =>
            {
                builder.HasKey(aggregate => aggregate.Id);
                builder.Ignore(aggregate => aggregate.DomainEvents);
            });
            modelBuilder.Entity<TestProjection>().HasKey(projection => projection.Id);
        }
    }

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        private TestAggregate()
        {
        }

        public TestAggregate(Guid id)
            : base(id)
        {
        }

        public int Revision { get; private set; }

        public void Touch()
        {
            this.Revision++;
            this.RaiseDomainEvent(new TestDomainEvent(Guid.NewGuid(), Now, this.Id));
        }
    }

    private sealed record TestDomainEvent(
        Guid EventId,
        DateTimeOffset OccurredAtUtc,
        Guid AggregateId) : IDomainEvent;

    private sealed class TestProjection
    {
        public Guid Id { get; init; }
        public Guid AggregateId { get; init; }
    }

    private sealed class RecordingDomainEventDispatcher(TestDbContext dbContext) : IDomainEventDispatcher
    {
        public List<IDomainEvent> DispatchedEvents { get; } = [];

        public Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            this.DispatchedEvents.AddRange(domainEvents);
            foreach (TestDomainEvent domainEvent in domainEvents.Cast<TestDomainEvent>())
            {
                dbContext.Projections.Add(new TestProjection
                {
                    Id = domainEvent.EventId,
                    AggregateId = domainEvent.AggregateId
                });
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Domain event dispatch failed.");
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.Parse("11111111-2222-3333-4444-555555555555");
    }
}
