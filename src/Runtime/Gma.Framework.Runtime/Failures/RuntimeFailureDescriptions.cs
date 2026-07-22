namespace Gma.Framework.Runtime.Failures;

using Gma.Framework.Naming;

public static class RuntimeFailureDescriptions
{
    public const int FailureCodeMaxLength = 64;
    public const int ExceptionTypeMaxLength = 128;

    public static string FromException(string failureCode, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string normalizedCode = SharedNameSegments.NormalizeKebabSegment(
            failureCode,
            "runtime failure code",
            nameof(failureCode));
        if (normalizedCode.Length > FailureCodeMaxLength)
        {
            throw new ArgumentException(
                $"{nameof(failureCode)} must be {FailureCodeMaxLength} characters or fewer.",
                nameof(failureCode));
        }

        string exceptionType = exception.GetType().Name;
        if (exceptionType.Length > ExceptionTypeMaxLength)
        {
            exceptionType = exceptionType[..ExceptionTypeMaxLength];
        }

        return $"{normalizedCode}:{exceptionType}";
    }
}
