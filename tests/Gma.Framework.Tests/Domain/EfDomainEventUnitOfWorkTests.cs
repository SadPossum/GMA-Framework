namespace Gma.Framework.Tests;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Application.Events;
using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class EfDomainEventUnitOfWorkTests
{
    [Fact]
    public async Task SaveChanges_dispatches_domain_events_before_saving_and_clears_after_success()
    {
        List<string> order = [];
        await using TestDbContext dbContext = CreateDbContext(order);
        RecordingDomainEventDispatcher dispatcher = new(order);
        TestUnitOfWork unitOfWork = new("test", dbContext, dispatcher);
        TestAggregate aggregate = new(Guid.NewGuid());
        aggregate.Touch();

        await dbContext.Aggregates.AddAsync(aggregate);
        await unitOfWork.SaveChangesAsync();

        Assert.Equal(["dispatch", "save"], order);
        Assert.Single(dispatcher.DispatchedEvents);
        Assert.Empty(aggregate.DomainEvents);
        Assert.Equal(1, await dbContext.Aggregates.CountAsync());
    }

    [Fact]
    public async Task SaveChanges_keeps_domain_events_when_dispatch_fails()
    {
        await using TestDbContext dbContext = CreateDbContext();
        ThrowingDomainEventDispatcher dispatcher = new();
        TestUnitOfWork unitOfWork = new("test", dbContext, dispatcher);
        TestAggregate aggregate = new(Guid.NewGuid());
        aggregate.Touch();

        await dbContext.Aggregates.AddAsync(aggregate);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());
        Assert.Single(aggregate.DomainEvents);
        Assert.False(dbContext.SaveWasCalled);
    }

    [Fact]
    public async Task SaveChanges_keeps_domain_events_when_commit_fails()
    {
        await using TestDbContext dbContext = CreateDbContext();
        dbContext.ThrowOnSave = true;
        RecordingDomainEventDispatcher dispatcher = new();
        TestUnitOfWork unitOfWork = new("test", dbContext, dispatcher);
        TestAggregate aggregate = new(Guid.NewGuid());
        aggregate.Touch();

        await dbContext.Aggregates.AddAsync(aggregate);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.SaveChangesAsync());
        Assert.Single(aggregate.DomainEvents);
        Assert.Single(dispatcher.DispatchedEvents);
    }

    [Fact]
    public async Task Failed_result_does_not_leak_tracked_mutations_or_events_to_next_command_in_scope()
    {
        await using TestDbContext dbContext = CreateDbContext();
        RecordingDomainEventDispatcher dispatcher = new();
        TestUnitOfWork unitOfWork = new("framework", dbContext, dispatcher);
        CommandUnitOfWorkBehavior<TestTransactionalCommand, Unit> behavior = new([unitOfWork]);
        TestAggregate abandoned = new(Guid.NewGuid());
        TestAggregate committed = new(Guid.NewGuid());
        Guid abandonedEventId = default;
        Guid committedEventId = default;

        Result<Unit> failed = await behavior.HandleAsync(
            new TestTransactionalCommand(),
            async () =>
            {
                abandonedEventId = abandoned.Touch();
                await dbContext.Aggregates.AddAsync(abandoned);
                return Result.Failure<Unit>(new Error("Test.Failure", "Failed."));
            },
            CancellationToken.None);

        Assert.Empty(dbContext.ChangeTracker.Entries());

        Result<Unit> succeeded = await behavior.HandleAsync(
            new TestTransactionalCommand(),
            async () =>
            {
                committedEventId = committed.Touch();
                await dbContext.Aggregates.AddAsync(committed);
                return Result.Success(Unit.Value);
            },
            CancellationToken.None);

        Assert.True(failed.IsFailure);
        Assert.True(succeeded.IsSuccess);
        Assert.Equal([committed.Id], await dbContext.Aggregates.Select(aggregate => aggregate.Id).ToArrayAsync());
        TestDomainEvent dispatched = Assert.IsType<TestDomainEvent>(Assert.Single(dispatcher.DispatchedEvents));
        Assert.Equal(committedEventId, dispatched.EventId);
        Assert.DoesNotContain(dispatcher.DispatchedEvents, domainEvent =>
            domainEvent is TestDomainEvent testEvent && testEvent.EventId == abandonedEventId);
    }

    [Fact]
    public async Task Handler_exception_does_not_leak_tracked_mutations_or_events_to_next_command_in_scope()
    {
        await using TestDbContext dbContext = CreateDbContext();
        RecordingDomainEventDispatcher dispatcher = new();
        TestUnitOfWork unitOfWork = new("framework", dbContext, dispatcher);
        CommandUnitOfWorkBehavior<TestTransactionalCommand, Unit> behavior = new([unitOfWork]);
        TestAggregate abandoned = new(Guid.NewGuid());
        TestAggregate committed = new(Guid.NewGuid());
        Guid abandonedEventId = default;
        Guid committedEventId = default;

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.HandleAsync(
            new TestTransactionalCommand(),
            async () =>
            {
                abandonedEventId = abandoned.Touch();
                await dbContext.Aggregates.AddAsync(abandoned);
                throw new InvalidOperationException("Handler failed.");
            },
            CancellationToken.None));

        Assert.Empty(dbContext.ChangeTracker.Entries());

        Result<Unit> succeeded = await behavior.HandleAsync(
            new TestTransactionalCommand(),
            async () =>
            {
                committedEventId = committed.Touch();
                await dbContext.Aggregates.AddAsync(committed);
                return Result.Success(Unit.Value);
            },
            CancellationToken.None);

        Assert.True(succeeded.IsSuccess);
        Assert.Equal([committed.Id], await dbContext.Aggregates.Select(aggregate => aggregate.Id).ToArrayAsync());
        TestDomainEvent dispatched = Assert.IsType<TestDomainEvent>(Assert.Single(dispatcher.DispatchedEvents));
        Assert.Equal(committedEventId, dispatched.EventId);
        Assert.DoesNotContain(dispatcher.DispatchedEvents, domainEvent =>
            domainEvent is TestDomainEvent testEvent && testEvent.EventId == abandonedEventId);
    }

    [Fact]
    public void Constructor_requires_module_name()
    {
        using TestDbContext dbContext = CreateDbContext();
        RecordingDomainEventDispatcher dispatcher = new();

        Assert.Throws<ArgumentException>(() => new TestUnitOfWork(" ", dbContext, dispatcher));
        Assert.Throws<ArgumentException>(() => new TestUnitOfWork("test.module", dbContext, dispatcher));
    }

    [Fact]
    public void Constructor_normalizes_module_name()
    {
        using TestDbContext dbContext = CreateDbContext();
        RecordingDomainEventDispatcher dispatcher = new();

        TestUnitOfWork unitOfWork = new(" Test ", dbContext, dispatcher);

        Assert.Equal("test", unitOfWork.ModuleName);
    }

    private static TestDbContext CreateDbContext(List<string>? order = null)
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options, order);
    }

    private sealed class TestUnitOfWork(
        string moduleName,
        TestDbContext dbContext,
        IDomainEventDispatcher dispatcher)
        : EfDomainEventUnitOfWork<TestDbContext>(moduleName, dbContext, dispatcher);

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, List<string>? order = null)
        : DbContext(options)
    {
        public DbSet<TestAggregate> Aggregates => this.Set<TestAggregate>();
        public bool ThrowOnSave { get; set; }
        public bool SaveWasCalled { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            this.SaveWasCalled = true;
            order?.Add("save");

            if (this.ThrowOnSave)
            {
                throw new InvalidOperationException("Commit failed.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestAggregate>(builder =>
            {
                builder.HasKey(aggregate => aggregate.Id);
                builder.Ignore(aggregate => aggregate.DomainEvents);
            });
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

        public Guid Touch()
        {
            Guid eventId = Guid.NewGuid();
            this.RaiseDomainEvent(new TestDomainEvent(eventId, DateTimeOffset.UtcNow));
            return eventId;
        }
    }

    private sealed record TestTransactionalCommand : ITransactionalCommand<Unit>;

    private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

    private sealed class RecordingDomainEventDispatcher(List<string>? order = null) : IDomainEventDispatcher
    {
        public List<IDomainEvent> DispatchedEvents { get; } = [];

        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
        {
            order?.Add("dispatch");
            this.DispatchedEvents.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Domain event dispatch failed.");
    }
}
