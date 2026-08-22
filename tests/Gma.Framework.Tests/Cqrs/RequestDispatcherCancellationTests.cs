namespace Gma.Framework.Tests;

using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RequestDispatcherCancellationTests
{
    [Fact]
    public async Task Pre_canceled_command_does_not_invoke_pipeline_or_handler()
    {
        ExecutionProbe probe = new();
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddScoped<ICommandHandler<TestCommand, Unit>, RecordingCommandHandler>();
            services.AddScoped<ICommandPipelineBehavior<TestCommand, Unit>, RecordingCommandBehavior>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.SendAsync(new TestCommand(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, probe.BehaviorCalls);
        Assert.Equal(0, probe.HandlerCalls);
    }

    [Fact]
    public async Task Pre_canceled_query_does_not_invoke_pipeline_or_handler()
    {
        ExecutionProbe probe = new();
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddScoped<IQueryHandler<TestQuery, Unit>, RecordingQueryHandler>();
            services.AddScoped<IQueryPipelineBehavior<TestQuery, Unit>, RecordingQueryBehavior>();
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.QueryAsync(new TestQuery(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, probe.BehaviorCalls);
        Assert.Equal(0, probe.HandlerCalls);
    }

    [Fact]
    public async Task Command_handler_result_is_not_accepted_after_cancellation()
    {
        using CancellationTokenSource cancellation = new();
        await using ServiceProvider provider = BuildProvider(services =>
            services.AddScoped<ICommandHandler<TestCommand, Unit>>(
                _ => new CancelingCommandHandler(cancellation.Cancel)));
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.SendAsync(new TestCommand(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Query_handler_result_is_not_accepted_after_cancellation()
    {
        using CancellationTokenSource cancellation = new();
        await using ServiceProvider provider = BuildProvider(services =>
            services.AddScoped<IQueryHandler<TestQuery, Unit>>(
                _ => new CancelingQueryHandler(cancellation.Cancel)));
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.QueryAsync(new TestQuery(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Command_behavior_result_is_not_accepted_after_cancellation()
    {
        ExecutionProbe probe = new();
        using CancellationTokenSource cancellation = new();
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddScoped<ICommandHandler<TestCommand, Unit>, RecordingCommandHandler>();
            services.AddScoped<ICommandPipelineBehavior<TestCommand, Unit>>(
                _ => new CancelingCommandBehavior(cancellation.Cancel));
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.SendAsync(new TestCommand(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, probe.HandlerCalls);
    }

    [Fact]
    public async Task Query_behavior_result_is_not_accepted_after_cancellation()
    {
        ExecutionProbe probe = new();
        using CancellationTokenSource cancellation = new();
        await using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton(probe);
            services.AddScoped<IQueryHandler<TestQuery, Unit>, RecordingQueryHandler>();
            services.AddScoped<IQueryPipelineBehavior<TestQuery, Unit>>(
                _ => new CancelingQueryBehavior(cancellation.Cancel));
        });
        using IServiceScope scope = provider.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.QueryAsync(new TestQuery(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, probe.HandlerCalls);
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configureServices)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = "Tests";
        builder.Configuration["Caching:Enabled"] = "false";
        builder.Configuration["Tenancy:Enabled"] = "false";

        builder.AddCqrsInfrastructure();
        configureServices(builder.Services);

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed record TestCommand : ICommand<Unit>;

    private sealed record TestQuery : IQuery<Unit>;

    private sealed class ExecutionProbe
    {
        public int BehaviorCalls { get; set; }

        public int HandlerCalls { get; set; }
    }

    private sealed class RecordingCommandHandler(ExecutionProbe probe) : ICommandHandler<TestCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            probe.HandlerCalls++;
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }

    private sealed class RecordingQueryHandler(ExecutionProbe probe) : IQueryHandler<TestQuery, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestQuery query, CancellationToken cancellationToken)
        {
            probe.HandlerCalls++;
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }

    private sealed class RecordingCommandBehavior(ExecutionProbe probe)
        : ICommandPipelineBehavior<TestCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            TestCommand command,
            CommandNext<Unit> next,
            CancellationToken cancellationToken)
        {
            probe.BehaviorCalls++;
            return next();
        }
    }

    private sealed class RecordingQueryBehavior(ExecutionProbe probe)
        : IQueryPipelineBehavior<TestQuery, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            TestQuery query,
            QueryNext<Unit> next,
            CancellationToken cancellationToken)
        {
            probe.BehaviorCalls++;
            return next();
        }
    }

    private sealed class CancelingCommandHandler(Action cancel) : ICommandHandler<TestCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            cancel();
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }

    private sealed class CancelingQueryHandler(Action cancel) : IQueryHandler<TestQuery, Unit>
    {
        public Task<Result<Unit>> HandleAsync(TestQuery query, CancellationToken cancellationToken)
        {
            cancel();
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }

    private sealed class CancelingCommandBehavior(Action cancel)
        : ICommandPipelineBehavior<TestCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            TestCommand command,
            CommandNext<Unit> next,
            CancellationToken cancellationToken)
        {
            cancel();
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }

    private sealed class CancelingQueryBehavior(Action cancel)
        : IQueryPipelineBehavior<TestQuery, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            TestQuery query,
            QueryNext<Unit> next,
            CancellationToken cancellationToken)
        {
            cancel();
            return Task.FromResult(Result.Success(Unit.Value));
        }
    }
}
