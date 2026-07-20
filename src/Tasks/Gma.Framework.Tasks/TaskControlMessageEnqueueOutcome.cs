namespace Gma.Framework.Tasks;

public enum TaskControlMessageEnqueueOutcome
{
    Unknown = 0,
    Enqueued = 1,
    AlreadyExists = 2,
    RunNotFound = 3,
    RunTerminal = 4,
    Conflict = 5
}
