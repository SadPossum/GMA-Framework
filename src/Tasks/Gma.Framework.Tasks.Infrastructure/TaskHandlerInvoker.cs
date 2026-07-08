namespace Gma.Framework.Tasks.Infrastructure;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Tasks;

internal static class TaskHandlerInvoker
{
    private const string HandleMethodName = "HandleAsync";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<HandlerKey, HandlerInvoker> Invokers = new();

    public static async Task InvokeAsync(
        IServiceProvider serviceProvider,
        TaskHandlerRegistration registration,
        string payloadJson,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(context);

        object payload = JsonSerializer.Deserialize(payloadJson, registration.PayloadType, SerializerOptions)
            ?? throw new InvalidOperationException(
                $"Task payload for {registration.ModuleName}.{registration.TaskName} deserialized to null.");

        object handler = serviceProvider.GetRequiredService(registration.HandlerType);
        HandlerInvoker invoker = Invokers.GetOrAdd(
            new HandlerKey(registration.PayloadType, registration.HandlerType),
            Create);

        Task? task = invoker(handler, payload, context, cancellationToken);
        if (task is null)
        {
            throw new InvalidOperationException($"Task handler {registration.HandlerType.FullName} returned a null task.");
        }

        await task.ConfigureAwait(false);
    }

    private static HandlerInvoker Create(HandlerKey key)
    {
        if (!typeof(ITaskPayload).IsAssignableFrom(key.PayloadType))
        {
            throw new InvalidOperationException(
                $"Task payload type {key.PayloadType.FullName} must implement {nameof(ITaskPayload)}.");
        }

        Type handlerInterface = typeof(ITaskHandler<>).MakeGenericType(key.PayloadType);
        if (!handlerInterface.IsAssignableFrom(key.HandlerType))
        {
            throw new InvalidOperationException(
                $"Task handler {key.HandlerType.FullName} must implement {handlerInterface.FullName}.");
        }

        MethodInfo handleMethod = handlerInterface.GetMethod(HandleMethodName)
            ?? throw new InvalidOperationException($"Task handler {key.HandlerType.FullName} has no {HandleMethodName} method.");

        ParameterExpression handler = Expression.Parameter(typeof(object), "handler");
        ParameterExpression payload = Expression.Parameter(typeof(object), "payload");
        ParameterExpression context = Expression.Parameter(typeof(TaskExecutionContext), "context");
        ParameterExpression cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        MethodCallExpression call = Expression.Call(
            Expression.Convert(handler, handlerInterface),
            handleMethod,
            Expression.Convert(payload, key.PayloadType),
            context,
            cancellationToken);

        return Expression
            .Lambda<HandlerInvoker>(
                call,
                handler,
                payload,
                context,
                cancellationToken)
            .Compile();
    }

    private delegate Task? HandlerInvoker(
        object handler,
        object payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken);

    private readonly record struct HandlerKey(Type PayloadType, Type HandlerType);
}
