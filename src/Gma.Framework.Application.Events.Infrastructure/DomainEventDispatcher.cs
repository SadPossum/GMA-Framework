namespace Gma.Framework.Application.Events.Infrastructure;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Application.Events;
using Gma.Framework.Domain;

internal sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, Func<DomainEventDispatcher, IDomainEvent, CancellationToken, Task>> Dispatchers = new();
    private static readonly MethodInfo DispatchTypedMethod = typeof(DomainEventDispatcher)
        .GetMethod(nameof(DispatchCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);
            Func<DomainEventDispatcher, IDomainEvent, CancellationToken, Task> dispatcher =
                Dispatchers.GetOrAdd(domainEvent.GetType(), CreateDispatcher);
            await dispatcher(this, domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchCoreAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        IEnumerable<IDomainEventHandler<TEvent>> handlers =
            serviceProvider.GetServices<IDomainEventHandler<TEvent>>();

        foreach (IDomainEventHandler<TEvent> handler in handlers)
        {
            Task? handlerTask = handler.HandleAsync(domainEvent, cancellationToken);
            if (handlerTask is null)
            {
                throw new InvalidOperationException(
                    $"Domain event handler '{handler.GetType().FullName}' returned a null task for event '{typeof(TEvent).FullName}'.");
            }

            await handlerTask.ConfigureAwait(false);
        }
    }

    private static Func<DomainEventDispatcher, IDomainEvent, CancellationToken, Task> CreateDispatcher(Type domainEventType)
    {
        ParameterExpression dispatcher = Expression.Parameter(typeof(DomainEventDispatcher), "dispatcher");
        ParameterExpression domainEvent = Expression.Parameter(typeof(IDomainEvent), "domainEvent");
        ParameterExpression cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
        MethodInfo method = DispatchTypedMethod.MakeGenericMethod(domainEventType);
        MethodCallExpression call = Expression.Call(
            dispatcher,
            method,
            Expression.Convert(domainEvent, domainEventType),
            cancellationToken);

        return Expression
            .Lambda<Func<DomainEventDispatcher, IDomainEvent, CancellationToken, Task>>(
                call,
                dispatcher,
                domainEvent,
                cancellationToken)
            .Compile();
    }
}
