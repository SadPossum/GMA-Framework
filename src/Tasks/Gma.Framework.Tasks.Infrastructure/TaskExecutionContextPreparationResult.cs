namespace Gma.Framework.Tasks.Infrastructure;

public sealed class TaskExecutionContextPreparationResult
{
    private TaskExecutionContextPreparationResult(bool isSuccess, string? errorMessage)
    {
        this.IsSuccess = isSuccess;
        this.ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !this.IsSuccess;
    public string? ErrorMessage { get; }

    public static TaskExecutionContextPreparationResult Success() => new(isSuccess: true, errorMessage: null);

    public static TaskExecutionContextPreparationResult Failure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new(isSuccess: false, errorMessage);
    }
}
