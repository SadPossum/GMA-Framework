namespace Gma.Framework.Tasks.Infrastructure;

public interface ITaskExecutionContextContributor
{
    ValueTask<TaskExecutionContextPreparationResult> PrepareAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken);

    ValueTask CleanupAsync(
        TaskExecutionContextPreparationContext context,
        CancellationToken cancellationToken);
}
