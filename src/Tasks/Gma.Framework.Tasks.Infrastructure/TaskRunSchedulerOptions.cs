namespace Gma.Framework.Tasks.Infrastructure;

public sealed class TaskRunSchedulerOptions
{
    public const string SectionName = "Tasks:Scheduler";

    public bool Enabled { get; set; }
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);
    public string RequestedBy { get; set; } = "task-scheduler";

    public TimeSpan EffectivePollInterval =>
        this.PollInterval > TimeSpan.Zero ? this.PollInterval : TimeSpan.FromSeconds(30);
}
