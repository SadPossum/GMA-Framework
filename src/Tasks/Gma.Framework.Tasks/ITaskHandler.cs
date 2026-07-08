namespace Gma.Framework.Tasks;

public interface ITaskHandler<in TPayload>
    where TPayload : ITaskPayload
{
    Task HandleAsync(
        TPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken);
}
