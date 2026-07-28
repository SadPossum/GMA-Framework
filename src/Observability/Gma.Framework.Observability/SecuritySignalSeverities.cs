namespace Gma.Framework.Observability;

public static class SecuritySignalSeverities
{
    public const string Notice = "notice";
    public const string Warning = "warning";
    public const string Critical = "critical";

    public static string ToWireName(SecuritySignalSeverity severity) =>
        severity switch
        {
            SecuritySignalSeverity.Notice => Notice,
            SecuritySignalSeverity.Warning => Warning,
            SecuritySignalSeverity.Critical => Critical,
            _ => throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "Security signal severity is invalid.")
        };
}
