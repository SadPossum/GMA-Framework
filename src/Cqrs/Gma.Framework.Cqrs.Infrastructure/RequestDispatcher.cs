namespace Gma.Framework.Cqrs.Infrastructure;

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class RequestDispatcher(IServiceProvider serviceProvider) : IRequestDispatcher
{
    public Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return CommandDispatchCache<TResponse>.Get(command.GetType())(this, command, cancellationToken);
    }

    public Task<Result<TResponse>> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return QueryDispatchCache<TResponse>.Get(query.GetType())(this, query, cancellationToken);
    }

    private async Task<Result<TResponse>> SendCoreAsync<TCommand, TResponse>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : ICommand<TResponse>
    {
        ICommandHandler<TCommand, TResponse> handler = this.GetRequiredCommandHandler<TCommand, TResponse>();

        IEnumerable<ICommandPipelineBehavior<TCommand, TResponse>> behaviors =
            serviceProvider.GetServices<ICommandPipelineBehavior<TCommand, TResponse>>();

        CommandNext<TResponse> next = () => EnsureResultAsync(
            handler.HandleAsync(command, cancellationToken),
            typeof(TCommand),
            handler.GetType(),
            "command handler");

        foreach (ICommandPipelineBehavior<TCommand, TResponse> behavior in behaviors.Reverse())
        {
            CommandNext<TResponse> current = next;
            next = () => EnsureResultAsync(
                behavior.HandleAsync(command, current, cancellationToken),
                typeof(TCommand),
                behavior.GetType(),
                "command pipeline behavior");
        }

        return await next().ConfigureAwait(false);
    }

    private async Task<Result<TResponse>> QueryCoreAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken cancellationToken)
        where TQuery : IQuery<TResponse>
    {
        IQueryHandler<TQuery, TResponse> handler = this.GetRequiredQueryHandler<TQuery, TResponse>();

        IEnumerable<IQueryPipelineBehavior<TQuery, TResponse>> behaviors =
            serviceProvider.GetServices<IQueryPipelineBehavior<TQuery, TResponse>>();

        QueryNext<TResponse> next = () => EnsureResultAsync(
            handler.HandleAsync(query, cancellationToken),
            typeof(TQuery),
            handler.GetType(),
            "query handler");

        foreach (IQueryPipelineBehavior<TQuery, TResponse> behavior in behaviors.Reverse())
        {
            QueryNext<TResponse> current = next;
            next = () => EnsureResultAsync(
                behavior.HandleAsync(query, current, cancellationToken),
                typeof(TQuery),
                behavior.GetType(),
                "query pipeline behavior");
        }

        return await next().ConfigureAwait(false);
    }

    private static async Task<Result<TResponse>> EnsureResultAsync<TResponse>(
        Task<Result<TResponse>>? resultTask,
        Type requestType,
        Type componentType,
        string componentKind)
    {
        if (resultTask is null)
        {
            throw new InvalidOperationException(
                $"The {componentKind} '{componentType.FullName}' returned a null result task for request '{requestType.FullName}'.");
        }

        Result<TResponse>? result = await resultTask.ConfigureAwait(false);
        return result ?? throw new InvalidOperationException(
            $"The {componentKind} '{componentType.FullName}' returned a null result for request '{requestType.FullName}'.");
    }

    private ICommandHandler<TCommand, TResponse> GetRequiredCommandHandler<TCommand, TResponse>()
        where TCommand : ICommand<TResponse>
    {
        ICommandHandler<TCommand, TResponse>[] handlers = serviceProvider
            .GetServices<ICommandHandler<TCommand, TResponse>>()
            .ToArray();

        return handlers.Length switch
        {
            1 => handlers[0],
            0 => throw new InvalidOperationException(
                $"No command handler is registered for request '{typeof(TCommand).FullName}'."),
            _ => throw new InvalidOperationException(
                $"{handlers.Length} command handlers are registered for request '{typeof(TCommand).FullName}'.")
        };
    }

    private IQueryHandler<TQuery, TResponse> GetRequiredQueryHandler<TQuery, TResponse>()
        where TQuery : IQuery<TResponse>
    {
        IQueryHandler<TQuery, TResponse>[] handlers = serviceProvider
            .GetServices<IQueryHandler<TQuery, TResponse>>()
            .ToArray();

        return handlers.Length switch
        {
            1 => handlers[0],
            0 => throw new InvalidOperationException(
                $"No query handler is registered for request '{typeof(TQuery).FullName}'."),
            _ => throw new InvalidOperationException(
                $"{handlers.Length} query handlers are registered for request '{typeof(TQuery).FullName}'.")
        };
    }

    private static class CommandDispatchCache<TResponse>
    {
        private static readonly ConcurrentDictionary<Type, Func<RequestDispatcher, ICommand<TResponse>, CancellationToken, Task<Result<TResponse>>>> Delegates = new();

        public static Func<RequestDispatcher, ICommand<TResponse>, CancellationToken, Task<Result<TResponse>>> Get(Type commandType) =>
            Delegates.GetOrAdd(commandType, Create);

        private static Func<RequestDispatcher, ICommand<TResponse>, CancellationToken, Task<Result<TResponse>>> Create(Type commandType)
        {
            ParameterExpression dispatcher = Expression.Parameter(typeof(RequestDispatcher), "dispatcher");
            ParameterExpression command = Expression.Parameter(typeof(ICommand<TResponse>), "command");
            ParameterExpression cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            MethodInfo method = typeof(RequestDispatcher)
                .GetMethod(nameof(SendCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(commandType, typeof(TResponse));

            MethodCallExpression call = Expression.Call(
                dispatcher,
                method,
                Expression.Convert(command, commandType),
                cancellationToken);

            return Expression
                .Lambda<Func<RequestDispatcher, ICommand<TResponse>, CancellationToken, Task<Result<TResponse>>>>(
                    call,
                    dispatcher,
                    command,
                    cancellationToken)
                .Compile();
        }
    }

    private static class QueryDispatchCache<TResponse>
    {
        private static readonly ConcurrentDictionary<Type, Func<RequestDispatcher, IQuery<TResponse>, CancellationToken, Task<Result<TResponse>>>> Delegates = new();

        public static Func<RequestDispatcher, IQuery<TResponse>, CancellationToken, Task<Result<TResponse>>> Get(Type queryType) =>
            Delegates.GetOrAdd(queryType, Create);

        private static Func<RequestDispatcher, IQuery<TResponse>, CancellationToken, Task<Result<TResponse>>> Create(Type queryType)
        {
            ParameterExpression dispatcher = Expression.Parameter(typeof(RequestDispatcher), "dispatcher");
            ParameterExpression query = Expression.Parameter(typeof(IQuery<TResponse>), "query");
            ParameterExpression cancellationToken = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

            MethodInfo method = typeof(RequestDispatcher)
                .GetMethod(nameof(QueryCoreAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(queryType, typeof(TResponse));

            MethodCallExpression call = Expression.Call(
                dispatcher,
                method,
                Expression.Convert(query, queryType),
                cancellationToken);

            return Expression
                .Lambda<Func<RequestDispatcher, IQuery<TResponse>, CancellationToken, Task<Result<TResponse>>>>(
                    call,
                    dispatcher,
                    query,
                    cancellationToken)
                .Compile();
        }
    }
}
