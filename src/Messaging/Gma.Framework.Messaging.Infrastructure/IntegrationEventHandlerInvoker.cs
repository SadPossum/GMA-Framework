namespace Gma.Framework.Messaging.Infrastructure;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Messaging;

public static class IntegrationEventHandlerInvoker
{
    private const string HandleMethodName = nameof(IIntegrationEventHandler<>.HandleAsync);

    private static readonly ConcurrentDictionary<HandlerKey, HandlerInvoker> Invokers = new();

    public static async Task InvokeAsync(
        IServiceProvider serviceProvider,
        IntegrationEventSubscription subscription,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(subscription);
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!subscription.EventType.IsInstanceOfType(integrationEvent))
        {
            throw new InvalidOperationException(
                $"Event {integrationEvent.GetType().FullName} cannot be handled by subscription for {subscription.EventType.FullName}.");
        }

        object handler = serviceProvider.GetRequiredService(subscription.HandlerType);
        HandlerInvoker invoker = Invokers.GetOrAdd(
            new HandlerKey(subscription.EventType, subscription.HandlerType),
            Create);

        Task? task = invoker(handler, integrationEvent, cancellationToken);
        if (task is null)
        {
            throw new InvalidOperationException($"Handler {subscription.HandlerType.FullName} returned a null task.");
        }

        await task.ConfigureAwait(false);
    }

    private static HandlerInvoker Create(HandlerKey key)
    {
        if (!typeof(IIntegrationEvent).IsAssignableFrom(key.EventType))
        {
            throw new InvalidOperationException(
                $"Event type {key.EventType.FullName} must implement {nameof(IIntegrationEvent)}.");
        }

        Type handlerInterface = typeof(IIntegrationEventHandler<>).MakeGenericType(key.EventType);
        if (!handlerInterface.IsAssignableFrom(key.HandlerType))
        {
            throw new InvalidOperationException(
                $"Handler {key.HandlerType.FullName} must implement {handlerInterface.FullName}.");
        }

        MethodInfo handleMethod = handlerInterface.GetMethod(HandleMethodName)
            ?? throw new InvalidOperationException($"Handler {key.HandlerType.FullName} has no {HandleMethodName} method.");

        ParameterExpression handler = Expression.Parameter(typeof(object), "handler");
        ParameterExpression integrationEvent = Expression.Parameter(typeof(IIntegrationEvent), "integrationEvent");
        ParameterExpression cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        MethodCallExpression call = Expression.Call(
            Expression.Convert(handler, handlerInterface),
            handleMethod,
            Expression.Convert(integrationEvent, key.EventType),
            cancellationToken);

        return Expression
            .Lambda<HandlerInvoker>(
                call,
                handler,
                integrationEvent,
                cancellationToken)
            .Compile();
    }

    private delegate Task? HandlerInvoker(
        object handler,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);

    private readonly record struct HandlerKey(Type EventType, Type HandlerType);
}
