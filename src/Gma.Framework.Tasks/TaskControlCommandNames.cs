namespace Gma.Framework.Tasks;

public static class TaskControlCommandNames
{
    public const string Cancel = "tasks.cancel";
    public const string Drain = "tasks.drain";
    public const string Pause = "tasks.pause";
    public const string Resume = "tasks.resume";

    public static bool IsStandard(string commandName)
    {
        string normalized = TaskNames.NormalizeControlCommandName(commandName, nameof(commandName));
        return normalized is Cancel or Drain or Pause or Resume;
    }

    public static bool IsCancellationSignal(string commandName)
    {
        string normalized = TaskNames.NormalizeControlCommandName(commandName, nameof(commandName));
        return normalized is Cancel or Drain;
    }
}
