namespace Gma.Framework.Administration;

public sealed class AdminOperationOptions
{
    public static readonly TimeSpan DefaultAuditWriteTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MinimumAuditWriteTimeout = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan MaximumAuditWriteTimeout = TimeSpan.FromMinutes(1);
    public const string InvalidConfigurationMessage =
        "The admin audit write timeout must be between 100 milliseconds and 1 minute.";

    public TimeSpan AuditWriteTimeout { get; set; } = DefaultAuditWriteTimeout;

    public static bool IsValid(AdminOperationOptions options) =>
        options.AuditWriteTimeout >= MinimumAuditWriteTimeout &&
        options.AuditWriteTimeout <= MaximumAuditWriteTimeout;
}
