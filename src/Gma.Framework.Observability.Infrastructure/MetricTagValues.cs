namespace Gma.Framework.Observability.Infrastructure;

using Gma.Framework.Naming;

public static class MetricTagValues
{
    private const int DimensionMaxLength = 128;
    private const int ErrorCodeMaxLength = 256;

    public static string Module(string moduleName) =>
        SharedNameSegments.NormalizeKebabSegment(moduleName, "module name", nameof(moduleName));

    public static string Operation(string operation) =>
        NormalizeDimension(operation, nameof(operation));

    public static string Provider(string provider) =>
        NormalizeDimension(provider, nameof(provider)).ToLowerInvariant();

    public static string Result(string result)
    {
        string normalized = NormalizeDimension(result, nameof(result)).ToLowerInvariant();
        return normalized switch
        {
            "success" or "failure" or "hit" or "miss" or "bypass" or "processed" or "duplicate" or "failed" or
            "claimed" or "canceled" or "timed-out" or "retry-scheduled" => normalized,
            _ => "unknown"
        };
    }

    public static string? ErrorCode(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return null;
        }

        string normalized = errorCode.Trim();
        if (normalized.Length > ErrorCodeMaxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"Metric error code tags must be {ErrorCodeMaxLength} characters or fewer and cannot contain whitespace or control characters.",
                nameof(errorCode));
        }

        return normalized;
    }

    private static string NormalizeDimension(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim();
        if (normalized.Length > DimensionMaxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"Metric {parameterName} tags must be {DimensionMaxLength} characters or fewer and cannot contain whitespace or control characters.",
                parameterName);
        }

        return normalized;
    }
}
