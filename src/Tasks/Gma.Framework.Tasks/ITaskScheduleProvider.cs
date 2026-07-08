namespace Gma.Framework.Tasks;

public interface ITaskScheduleProvider
{
    Task<IReadOnlyList<ScheduledTaskDefinition>> GetSchedulesAsync(CancellationToken cancellationToken);
}
