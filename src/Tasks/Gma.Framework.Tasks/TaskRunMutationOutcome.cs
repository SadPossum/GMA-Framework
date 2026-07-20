namespace Gma.Framework.Tasks;

public enum TaskRunMutationOutcome
{
    Unknown = 0,
    Applied = 1,
    AlreadyApplied = 2,
    NotFound = 3,
    InvalidState = 4,
    LeaseLost = 5,
    Conflict = 6,
    InvalidRequest = 7
}
