namespace Gma.Framework.Cqrs;

using Gma.Framework.Results;

public static class RequestValidationErrors
{
    public const string FailedCode = "Validation.Failed";

    public static Error Failed(IEnumerable<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        string[] normalizedFailures = failures
            .Where(failure => !string.IsNullOrWhiteSpace(failure))
            .Select(failure => failure.Trim())
            .ToArray();
        string message = normalizedFailures.Length == 0
            ? "Validation failed."
            : string.Join("; ", normalizedFailures);

        return new Error(FailedCode, message);
    }
}
