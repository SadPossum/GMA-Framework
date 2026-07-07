namespace Gma.Framework.Administration;

using Gma.Framework.Results;

public sealed record AdminOperationExecutionResult<T>
{
    public AdminOperationExecutionResult(
        AdminOperationExecutionStatus status,
        Result<T> result,
        string? auditError)
    {
        if (!Enum.IsDefined(status) || status == AdminOperationExecutionStatus.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Admin operation execution status must be a known non-unknown value.");
        }

        ArgumentNullException.ThrowIfNull(result);

        if (status == AdminOperationExecutionStatus.Succeeded && result.IsFailure)
        {
            throw new ArgumentException("A successful admin operation execution result must carry a successful result.", nameof(result));
        }

        if (status != AdminOperationExecutionStatus.Succeeded && result.IsSuccess)
        {
            throw new ArgumentException("A failed admin operation execution result must carry a failed result.", nameof(result));
        }

        this.Status = status;
        this.Result = result;
        this.AuditError = NormalizeAuditError(auditError);
    }

    public AdminOperationExecutionStatus Status { get; }
    public Result<T> Result { get; }
    public string? AuditError { get; }
    public bool IsSuccess => this.Status == AdminOperationExecutionStatus.Succeeded;

    private static string? NormalizeAuditError(string? auditError)
    {
        if (auditError is null)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(auditError);

        string normalized = auditError.Trim();
        if (normalized.Length > Error.MessageMaxLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Audit error must be {Error.MessageMaxLength} characters or fewer and cannot contain control characters.",
                nameof(auditError));
        }

        return normalized;
    }
}
