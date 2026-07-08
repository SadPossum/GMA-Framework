namespace Gma.Framework.Tasks;

public sealed record TaskControlPollResult(IReadOnlyList<TaskControlMessage> Messages)
{
    public bool HasMessages => this.Messages.Count > 0;

    public bool CancelRequested => this.HasCommand(TaskControlCommandNames.Cancel);

    public bool DrainRequested => this.HasCommand(TaskControlCommandNames.Drain);

    public bool PauseRequested => this.HasCommand(TaskControlCommandNames.Pause);

    public bool ResumeRequested => this.HasCommand(TaskControlCommandNames.Resume);

    public TaskControlMessage? CancellationMessage =>
        this.Messages.FirstOrDefault(message => TaskControlCommandNames.IsCancellationSignal(message.CommandName));

    public bool HasCommand(string commandName)
    {
        string normalized = TaskNames.NormalizeControlCommandName(commandName, nameof(commandName));
        return this.Messages.Any(message => string.Equals(message.CommandName, normalized, StringComparison.Ordinal));
    }
}
