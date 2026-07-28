namespace Gma.Framework.Tests;

using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CommandOutcomeObservationBehaviorTests
{
    [Fact]
    public async Task Observer_runs_after_the_inner_pipeline_settles()
    {
        List<string> order = [];
        RecordingObserver observer = new(order);
        CommandOutcomeObservationBehavior<TestCommand, Unit> behavior = new(
            [observer],
            Options.Create(new CommandOutcomeObservationOptions()),
            NullLogger<CommandOutcomeObservationBehavior<TestCommand, Unit>>.Instance);

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () =>
            {
                order.Add("committed");
                return Task.FromResult(Result.Success(Unit.Value));
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["committed", "observed"], order);
        Assert.Same(result, observer.Result);
    }

    [Fact]
    public async Task Observer_failure_does_not_change_the_settled_result()
    {
        Result<Unit> expected = Result.Success(Unit.Value);
        CommandOutcomeObservationBehavior<TestCommand, Unit> behavior = new(
            [new ThrowingObserver()],
            Options.Create(new CommandOutcomeObservationOptions()),
            NullLogger<CommandOutcomeObservationBehavior<TestCommand, Unit>>.Instance);

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Logging_failure_does_not_change_the_settled_result()
    {
        Result<Unit> expected = Result.Success(Unit.Value);
        CommandOutcomeObservationBehavior<TestCommand, Unit> behavior = new(
            [new ThrowingObserver()],
            Options.Create(new CommandOutcomeObservationOptions()),
            new ThrowingLogger<CommandOutcomeObservationBehavior<TestCommand, Unit>>());

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Observation_timeout_does_not_change_the_settled_result()
    {
        Result<Unit> expected = Result.Success(Unit.Value);
        CommandOutcomeObservationBehavior<TestCommand, Unit> behavior = new(
            [new WaitingObserver()],
            Options.Create(new CommandOutcomeObservationOptions
            {
                Timeout = CommandOutcomeObservationOptions.MinimumTimeout
            }),
            NullLogger<CommandOutcomeObservationBehavior<TestCommand, Unit>>.Instance);

        Result<Unit> result = await behavior.HandleAsync(
            new TestCommand(),
            () => Task.FromResult(expected),
            CancellationToken.None);

        Assert.Same(expected, result);
    }

    private sealed record TestCommand : ICommand<Unit>;

    private sealed class RecordingObserver(List<string> order)
        : ICommandOutcomeObserver<TestCommand, Unit>
    {
        public Result<Unit>? Result { get; private set; }

        public Task ObserveAsync(
            TestCommand command,
            Result<Unit> result,
            CancellationToken cancellationToken)
        {
            this.Result = result;
            order.Add("observed");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingObserver : ICommandOutcomeObserver<TestCommand, Unit>
    {
        public Task ObserveAsync(
            TestCommand command,
            Result<Unit> result,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("observer unavailable");
    }

    private sealed class WaitingObserver : ICommandOutcomeObserver<TestCommand, Unit>
    {
        public Task ObserveAsync(
            TestCommand command,
            Result<Unit> result,
            CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("logger unavailable");
    }
}
