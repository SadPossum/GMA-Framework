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
}
