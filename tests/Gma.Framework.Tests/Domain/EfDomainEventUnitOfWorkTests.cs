namespace Gma.Framework.Tests;

using Microsoft.EntityFrameworkCore;
using Gma.Framework.Application.Events;
using Gma.Framework.Domain;
using Gma.Framework.Domain.Models;
using Gma.Framework.Persistence.EntityFrameworkCore;
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

        public void Touch() => this.RaiseDomainEvent(new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

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
