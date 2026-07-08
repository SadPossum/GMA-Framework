namespace Gma.Framework.Administration;

public enum AdminOperationExecutionStatus
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Unauthorized = 3,
    ValidationFailed = 4,
    UnexpectedFailure = 5
}
