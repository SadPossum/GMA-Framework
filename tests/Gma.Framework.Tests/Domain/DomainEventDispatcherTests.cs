namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Application.Events;
using Gma.Framework.Domain;
using Gma.Framework.Application.Events.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_invokes_matching_handlers_in_registration_order()
    {
        ServiceCollection services = new();
        services.AddSingleton<DispatchTrace>();
        services.AddTransient<IDomainEventHandler<TestDomainEvent>, FirstDomainEventHandler>();
        services.AddTransient<IDomainEventHandler<TestDomainEvent>, SecondDomainEventHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        DomainEventDispatcher dispatcher = new(provider);
        TestDomainEvent domainEvent = new(Guid.NewGuid(), DateTimeOffset.UtcNow);

        await dispatcher.DispatchAsync([domainEvent], CancellationToken.None);

        DispatchTrace trace = provider.GetRequiredService<DispatchTrace>();
        Assert.Equal(["first", "second"], trace.Entries);
    }

    [Fact]
    public async Task DispatchAsync_ignores_events_without_handlers()
    {
        await using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        DomainEventDispatcher dispatcher = new(provider);

        await dispatcher.DispatchAsync(
            [new UnhandledDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow)],
            CancellationToken.None);
    }

    [Fact]
    public async Task DispatchAsync_preserves_handler_exception()
    {
        ServiceCollection services = new();
        services.AddTransient<IDomainEventHandler<TestDomainEvent>, ThrowingDomainEventHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        DomainEventDispatcher dispatcher = new(provider);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(
                [new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow)],
                CancellationToken.None));

        Assert.Equal("domain event handler failed", exception.Message);
    }

    [Fact]
    public async Task DispatchAsync_rejects_null_event_entries()
    {
        await using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        DomainEventDispatcher dispatcher = new(provider);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            dispatcher.DispatchAsync([null!], CancellationToken.None));
    }

    [Fact]
    public async Task DispatchAsync_rejects_null_handler_task()
    {
        ServiceCollection services = new();
        services.AddTransient<IDomainEventHandler<TestDomainEvent>, NullTaskDomainEventHandler>();
        await using ServiceProvider provider = services.BuildServiceProvider();
        DomainEventDispatcher dispatcher = new(provider);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(
                [new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow)],
                CancellationToken.None));

        Assert.Contains(nameof(NullTaskDomainEventHandler), exception.Message, StringComparison.Ordinal);
        Assert.Contains("returned a null task", exception.Message, StringComparison.Ordinal);
    }

    private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

    private sealed record UnhandledDomainEvent(Guid EventId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

    private sealed class DispatchTrace
    {
        public List<string> Entries { get; } = [];
    }

    private sealed class FirstDomainEventHandler(DispatchTrace trace) : IDomainEventHandler<TestDomainEvent>
    {
        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            trace.Entries.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondDomainEventHandler(DispatchTrace trace) : IDomainEventHandler<TestDomainEvent>
    {
        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            trace.Entries.Add("second");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDomainEventHandler : IDomainEventHandler<TestDomainEvent>
    {
        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("domain event handler failed");
    }

    private sealed class NullTaskDomainEventHandler : IDomainEventHandler<TestDomainEvent>
    {
        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken) =>
            null!;
    }
}
