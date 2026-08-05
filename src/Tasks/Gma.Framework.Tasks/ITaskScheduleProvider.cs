namespace Gma.Framework.Tasks;

public interface ITaskScheduleProvider
{
    IAsyncEnumerable<ScheduledTaskDefinition> GetSchedulesAsync(
        CancellationToken cancellationToken);
}
