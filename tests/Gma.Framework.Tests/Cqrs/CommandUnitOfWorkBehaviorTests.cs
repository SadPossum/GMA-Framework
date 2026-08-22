namespace Gma.Framework.Tests;

using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Results;
using Gma.Framework.Cqrs.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CommandUnitOfWorkBehaviorTests
{
    [Fact]
    public async Task Non_transactional_command_does_not_commit()
    {
        RecordingUnitOfWork unitOfWork = new("framework");
        CommandUnitOfWorkBehavior<NonTransactionalCommand, Unit> behavior = new([unitOfWork]);

        Result<Unit> result = await behavior.HandleAsync(
            new NonTransactionalCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, unitOfWork.Commits);
    }

    [Fact]
    public async Task Transactional_command_commits_only_matching_module()
    {
        RecordingUnitOfWork matching = new(" Framework ");
        RecordingUnitOfWork other = new("auth");
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([other, matching]);

        Result<Unit> result = await behavior.HandleAsync(
            new TransactionalCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, matching.Commits);
        Assert.Equal(0, other.Commits);
    }

    [Fact]
    public async Task Transactional_unit_of_work_wraps_handler_and_save_in_one_boundary()
    {
        List<string> order = [];
        RecordingTransactionalUnitOfWork unitOfWork = new("framework", order);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        Result<Unit> result = await behavior.HandleAsync(
            new TransactionalCommand(),
            () =>
            {
                order.Add("handle");
                return Task.FromResult(Result.Success(Unit.Value));
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["begin", "handle", "save", "commit"], order);
    }

    [Fact]
    public async Task Transactional_unit_of_work_stops_after_cancellation_ignoring_begin()
    {
        List<string> order = [];
        using CancellationTokenSource cancellation = new();
        RecordingTransactionalUnitOfWork unitOfWork = new(
            "framework",
            order,
            afterBegin: cancellation.Cancel);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => behavior.HandleAsync(
                new TransactionalCommand(),
                () =>
                {
                    order.Add("handle");
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(["begin", "rollback", "reset"], order);
    }

    [Fact]
    public async Task Transactional_unit_of_work_stops_before_save_after_inner_cancellation()
    {
        List<string> order = [];
        using CancellationTokenSource cancellation = new();
        RecordingTransactionalUnitOfWork unitOfWork = new("framework", order);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => behavior.HandleAsync(
                new TransactionalCommand(),
                () =>
                {
                    order.Add("handle");
                    cancellation.Cancel();
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(["begin", "handle", "rollback", "reset"], order);
    }

    [Fact]
    public async Task Transactional_unit_of_work_stops_before_commit_after_cancellation_ignoring_save()
    {
        List<string> order = [];
        using CancellationTokenSource cancellation = new();
        RecordingTransactionalUnitOfWork unitOfWork = new(
            "framework",
            order,
            afterSave: cancellation.Cancel);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => behavior.HandleAsync(
                new TransactionalCommand(),
                () =>
                {
                    order.Add("handle");
                    return Task.FromResult(Result.Success(Unit.Value));
                },
                cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(["begin", "handle", "save", "rollback", "reset"], order);
    }

    [Fact]
    public async Task Transactional_unit_of_work_rolls_back_failed_results_without_saving()
    {
        List<string> order = [];
        RecordingTransactionalUnitOfWork unitOfWork = new("framework", order);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        Result<Unit> result = await behavior.HandleAsync(
            new TransactionalCommand(),
            () =>
            {
                order.Add("handle");
                return Task.FromResult(Result.Failure<Unit>(new Error("Test.Failure", "Failed.")));
            },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(["begin", "handle", "rollback", "reset"], order);
    }

    [Fact]
    public async Task Transactional_unit_of_work_rolls_back_handler_exceptions()
    {
        List<string> order = [];
        RecordingTransactionalUnitOfWork unitOfWork = new("framework", order);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.HandleAsync(
            new TransactionalCommand(),
            () =>
            {
                order.Add("handle");
                throw new InvalidOperationException("Handler failed.");
            },
            CancellationToken.None));

        Assert.Equal(["begin", "handle", "rollback", "reset"], order);
    }

    [Fact]
    public async Task Result_failure_attempts_reset_when_rollback_fails()
    {
        List<string> order = [];
        InvalidOperationException rollbackFailure = new("Rollback failed.");
        RecordingTransactionalUnitOfWork unitOfWork = new(
            "framework",
            order,
            rollbackFailure: rollbackFailure);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.HandleAsync(
            new TransactionalCommand(),
            () =>
            {
                order.Add("handle");
                return Task.FromResult(Result.Failure<Unit>(new Error("Test.Failure", "Failed.")));
            },
            CancellationToken.None));

        Assert.Same(rollbackFailure, actual);
        Assert.Equal(["begin", "handle", "rollback", "reset"], order);
    }

    [Fact]
    public async Task Handler_exception_is_preserved_when_rollback_and_reset_fail()
    {
        List<string> order = [];
        InvalidOperationException handlerFailure = new("Handler failed.");
        InvalidOperationException rollbackFailure = new("Rollback failed.");
        InvalidOperationException resetFailure = new("Reset failed.");
        RecordingTransactionalUnitOfWork unitOfWork = new(
            "framework",
            order,
            rollbackFailure,
            resetFailure);
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([unitOfWork]);

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.HandleAsync(
            new TransactionalCommand(),
            () =>
            {
                order.Add("handle");
                throw handlerFailure;
            },
            CancellationToken.None));

        Assert.Same(handlerFailure, actual);
        AggregateException cleanupFailure = Assert.IsType<AggregateException>(
            actual.Data[CommandUnitOfWorkBehavior<TransactionalCommand, Unit>.CleanupFailureDataKey]);
        Assert.Equal([rollbackFailure, resetFailure], cleanupFailure.InnerExceptions);
        Assert.Equal(["begin", "handle", "rollback", "reset"], order);
    }

    [Fact]
    public async Task Transactional_command_treats_normalized_module_names_as_duplicates()
    {
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> duplicate = new(
            [new RecordingUnitOfWork("framework"), new RecordingUnitOfWork(" Framework ")]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => duplicate.HandleAsync(
            new TransactionalCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None));
    }

    [Fact]
    public async Task Transactional_command_requires_exactly_one_matching_unit_of_work()
    {
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> missing = new([new RecordingUnitOfWork("auth")]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.HandleAsync(
            new TransactionalCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None));

        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> duplicate = new(
            [new RecordingUnitOfWork("framework"), new RecordingUnitOfWork("framework")]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => duplicate.HandleAsync(
            new TransactionalCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None));
    }

    [Fact]
    public async Task Transactional_command_rejects_malformed_unit_of_work_module_name()
    {
        CommandUnitOfWorkBehavior<TransactionalCommand, Unit> behavior = new([new RecordingUnitOfWork("shared.module")]);

        await Assert.ThrowsAsync<ArgumentException>(() => behavior.HandleAsync(
            new TransactionalCommand(),
            () => Task.FromResult(Result.Success(Unit.Value)),
            CancellationToken.None));
    }

    private sealed record NonTransactionalCommand : ICommand<Unit>;

    private sealed record TransactionalCommand : ITransactionalCommand<Unit>;

    private sealed class RecordingUnitOfWork(string moduleName) : IUnitOfWork
    {
        public string ModuleName { get; } = moduleName;
        public int Commits { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            this.Commits++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTransactionalUnitOfWork(
        string moduleName,
        List<string> order,
        Exception? rollbackFailure = null,
        Exception? resetFailure = null,
        Action? afterBegin = null,
        Action? afterSave = null)
        : IRollbackResettableUnitOfWork
    {
        public string ModuleName { get; } = moduleName;

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            order.Add("begin");
            afterBegin?.Invoke();
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            order.Add("save");
            afterSave?.Invoke();
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            order.Add("commit");
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            order.Add("rollback");
            return rollbackFailure is null
                ? Task.CompletedTask
                : Task.FromException(rollbackFailure);
        }

        public Task ResetAfterRollbackAsync(CancellationToken cancellationToken = default)
        {
            order.Add("reset");
            return resetFailure is null
                ? Task.CompletedTask
                : Task.FromException(resetFailure);
        }
    }
}
