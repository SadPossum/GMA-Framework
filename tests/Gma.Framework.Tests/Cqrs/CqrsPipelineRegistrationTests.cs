namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Cqrs.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CqrsPipelineRegistrationTests
{
    [Fact]
    public async Task Cqrs_infrastructure_registers_query_behaviors_in_expected_order()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        Type[] behaviorTypes = scope.ServiceProvider
            .GetServices<IQueryPipelineBehavior<TestQuery, Unit>>()
            .Select(behavior => behavior.GetType())
            .ToArray();

        Assert.Equal(
            [
                typeof(ValidationQueryBehavior<TestQuery, Unit>),
                typeof(LoggingQueryBehavior<TestQuery, Unit>)
            ],
            behaviorTypes);
    }

    [Fact]
    public async Task Request_dispatcher_runs_query_pipeline_before_handler()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<QueryTrace>();
            services.AddScoped<RecordingQueryHandler>();
            services.AddScoped<IQueryHandler<TestQuery, Unit>>(provider => provider.GetRequiredService<RecordingQueryHandler>());
            services.AddScoped<IQueryPipelineBehavior<TestQuery, Unit>, RecordingQueryBehavior>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<Unit> result = await dispatcher.QueryAsync(new TestQuery());

        Assert.True(result.IsSuccess);
        RecordingQueryHandler handler = scope.ServiceProvider.GetRequiredService<RecordingQueryHandler>();
        QueryTrace trace = scope.ServiceProvider.GetRequiredService<QueryTrace>();
        Assert.Equal(["before", "handler", "after"], trace.Order);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Request_dispatcher_returns_query_validation_failure_before_handler()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<QueryTrace>();
            services.AddScoped<RecordingQueryHandler>();
            services.AddScoped<IQueryHandler<TestQuery, Unit>>(provider => provider.GetRequiredService<RecordingQueryHandler>());
            services.AddScoped<IQueryValidator<TestQuery>, FailingQueryValidator>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<Unit> result = await dispatcher.QueryAsync(new TestQuery());

        Assert.True(result.IsFailure);
        Assert.Equal(RequestValidationErrors.FailedCode, result.Error.Code);
        Assert.Equal("Query is invalid.", result.Error.Message);
        RecordingQueryHandler handler = scope.ServiceProvider.GetRequiredService<RecordingQueryHandler>();
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Request_dispatcher_returns_command_validation_failure_before_handler()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<RecordingCommandHandler>();
            services.AddScoped<ICommandHandler<TestCommand, Unit>>(provider => provider.GetRequiredService<RecordingCommandHandler>());
            services.AddScoped<ICommandValidator<TestCommand>, FailingCommandValidator>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<Unit> result = await dispatcher.SendAsync(new TestCommand());

        Assert.True(result.IsFailure);
        Assert.Equal(RequestValidationErrors.FailedCode, result.Error.Code);
        Assert.Equal("Command is invalid.", result.Error.Message);
        RecordingCommandHandler handler = scope.ServiceProvider.GetRequiredService<RecordingCommandHandler>();
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_null_command_validator_failure_collection()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, Unit>, RecordingCommandHandler>();
            services.AddScoped<ICommandValidator<TestCommand>, NullFailuresCommandValidator>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new TestCommand()));

        Assert.Contains(nameof(NullFailuresCommandValidator), exception.Message, StringComparison.Ordinal);
        Assert.Contains("returned a null failure collection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_null_query_validator_failure_collection()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<IQueryHandler<TestQuery, Unit>, RecordingQueryHandler>();
            services.AddScoped<IQueryValidator<TestQuery>, NullFailuresQueryValidator>();
            services.AddScoped<QueryTrace>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.QueryAsync(new TestQuery()));

        Assert.Contains(nameof(NullFailuresQueryValidator), exception.Message, StringComparison.Ordinal);
        Assert.Contains("returned a null failure collection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_null_command_handler_result_task()
    {
        await using ServiceProvider provider = BuildProvider(services =>
            services.AddScoped<ICommandHandler<TestCommand, Unit>, NullTaskCommandHandler>());
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new TestCommand()));

        Assert.Contains("command handler", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(NullTaskCommandHandler), exception.Message, StringComparison.Ordinal);
        Assert.Contains("null result task", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_null_query_handler_result()
    {
        await using ServiceProvider provider = BuildProvider(services =>
            services.AddScoped<IQueryHandler<TestQuery, Unit>, NullResultQueryHandler>());
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.QueryAsync(new TestQuery()));

        Assert.Contains("query handler", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(NullResultQueryHandler), exception.Message, StringComparison.Ordinal);
        Assert.Contains("null result", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_null_command_pipeline_result()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, Unit>, RecordingCommandHandler>();
            services.AddScoped<ICommandPipelineBehavior<TestCommand, Unit>, NullResultCommandBehavior>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new TestCommand()));

        Assert.Contains("command pipeline behavior", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(NullResultCommandBehavior), exception.Message, StringComparison.Ordinal);
        Assert.Contains("null result", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_null_query_pipeline_result_task()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<IQueryHandler<TestQuery, Unit>, RecordingQueryHandler>();
            services.AddScoped<QueryTrace>();
            services.AddScoped<IQueryPipelineBehavior<TestQuery, Unit>, NullTaskQueryBehavior>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.QueryAsync(new TestQuery()));

        Assert.Contains("query pipeline behavior", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(NullTaskQueryBehavior), exception.Message, StringComparison.Ordinal);
        Assert.Contains("null result task", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_requires_a_registered_command_handler()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new TestCommand()));

        Assert.Contains("No command handler is registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestCommand), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_requires_a_registered_query_handler()
    {
        await using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.QueryAsync(new TestQuery()));

        Assert.Contains("No query handler is registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestQuery), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_duplicate_command_handlers()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, Unit>, RecordingCommandHandler>();
            services.AddScoped<ICommandHandler<TestCommand, Unit>, OtherCommandHandler>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new TestCommand()));

        Assert.Contains("2 command handlers are registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestCommand), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_dispatcher_rejects_duplicate_query_handlers()
    {
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddScoped<IQueryHandler<TestQuery, Unit>, RecordingQueryHandler>();
            services.AddScoped<IQueryHandler<TestQuery, Unit>, OtherQueryHandler>();
            services.AddScoped<QueryTrace>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.QueryAsync(new TestQuery()));

        Assert.Contains("2 query handlers are registered", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestQuery), exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection>? configureServices = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = "Tests";
        builder.Configuration["Caching:Enabled"] = "false";
        builder.Configuration["Tenancy:Enabled"] = "false";

        builder.AddCqrsInfrastructure();
        configureServices?.Invoke(builder.Services);

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed record TestQuery : IQuery<Unit>;
    private sealed record TestCommand : ICommand<Unit>;

    private sealed class QueryTrace
    {
        public List<string> Order { get; } = [];
    }

    private sealed class RecordingQueryHandler(QueryTrace trace) : IQueryHandler<TestQuery, Unit>
    {
        public int Calls { get; private set; }

        public Task<Result<Unit>> HandleAsync(TestQuery query, CancellationToken cancellationToken)
        {
            this.Calls++;
            trace.Order.Add("handler");
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }

    private sealed class RecordingCommandHandler : ICommandHandler<TestCommand, Unit>
    {
        public int Calls { get; private set; }

        public Task<Result<Unit>> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            this.Calls++;
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }

    private sealed class OtherCommandHandler : ICommandHandler<TestCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(Unit.Value));
    }

    private sealed class OtherQueryHandler : IQueryHandler<TestQuery, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(Unit.Value));
    }

    private sealed class NullTaskCommandHandler : ICommandHandler<TestCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestCommand command, CancellationToken cancellationToken) => null!;
    }

    private sealed class NullResultQueryHandler : IQueryHandler<TestQuery, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<Result<Unit>>(null!);
    }

    private sealed class RecordingQueryBehavior(QueryTrace trace) : IQueryPipelineBehavior<TestQuery, Unit>
    {
        public async Task<Result<Unit>> HandleAsync(
            TestQuery query,
            QueryNext<Unit> next,
            CancellationToken cancellationToken)
        {
            trace.Order.Add("before");
            Result<Unit> result = await next().ConfigureAwait(false);
            trace.Order.Add("after");
            return result;
        }
    }

    private sealed class NullResultCommandBehavior : ICommandPipelineBehavior<TestCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            TestCommand command,
            CommandNext<Unit> next,
            CancellationToken cancellationToken) =>
            Task.FromResult<Result<Unit>>(null!);
    }

    private sealed class NullTaskQueryBehavior : IQueryPipelineBehavior<TestQuery, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            TestQuery query,
            QueryNext<Unit> next,
            CancellationToken cancellationToken) =>
            null!;
    }

    private sealed class FailingQueryValidator : IQueryValidator<TestQuery>
    {
        public IEnumerable<string> Validate(TestQuery query)
        {
            yield return "Query is invalid.";
        }
    }

    private sealed class FailingCommandValidator : ICommandValidator<TestCommand>
    {
        public IEnumerable<string> Validate(TestCommand command)
        {
            yield return "Command is invalid.";
        }
    }

    private sealed class NullFailuresCommandValidator : ICommandValidator<TestCommand>
    {
        public IEnumerable<string> Validate(TestCommand command) => null!;
    }

    private sealed class NullFailuresQueryValidator : IQueryValidator<TestQuery>
    {
        public IEnumerable<string> Validate(TestQuery query) => null!;
    }
}
