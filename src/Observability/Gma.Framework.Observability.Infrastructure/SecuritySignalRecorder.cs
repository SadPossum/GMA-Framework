namespace Gma.Framework.Observability.Infrastructure;

using Gma.Framework.Observability;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.Logging;

internal sealed partial class SecuritySignalRecorder(
    SecuritySignalRegistry registry,
    SecuritySignalMetrics metrics,
    ISystemClock clock,
    ILogger<SecuritySignalRecorder> logger)
    : ISecuritySignalRecorder
{
    public SecuritySignalReceipt Record(
        SecuritySignalDefinition definition,
        Guid? correlationId = null)
    {
        SecuritySignalDefinition registered = registry.Require(definition);
        SecuritySignalRecord record = new(
            registered,
            SecuritySignalCorrelation.Create(correlationId),
            clock.UtcNow);
        bool emitted = false;

        try
        {
            metrics.Record(registered);
            emitted = true;
        }
        catch (Exception)
        {
            // Observability providers must not change the protected operation.
        }

        try
        {
            this.WriteLog(record);
            emitted = true;
        }
        catch (Exception)
        {
            // The metric path may still have recorded the occurrence.
        }

        return new(record.IncidentCorrelationId, emitted);
    }

    private void WriteLog(SecuritySignalRecord record)
    {
        string category = SecuritySignalCategories.ToWireName(record.Category);
        string severity = SecuritySignalSeverities.ToWireName(record.Severity);

        switch (record.Severity)
        {
            case SecuritySignalSeverity.Notice:
                LogNotice(
                    logger,
                    record.SignalCode,
                    category,
                    severity,
                    record.IncidentCorrelationId,
                    record.OccurredAtUtc);
                break;
            case SecuritySignalSeverity.Warning:
                LogWarning(
                    logger,
                    record.SignalCode,
                    category,
                    severity,
                    record.IncidentCorrelationId,
                    record.OccurredAtUtc);
                break;
            case SecuritySignalSeverity.Critical:
                LogCritical(
                    logger,
                    record.SignalCode,
                    category,
                    severity,
                    record.IncidentCorrelationId,
                    record.OccurredAtUtc);
                break;
            case SecuritySignalSeverity.Unknown:
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(record),
                    record.Severity,
                    "Security signal severity is invalid.");
        }
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message =
            "Security signal {SecuritySignalCode} in category {SecuritySignalCategory} " +
            "at severity {SecuritySignalSeverity}; incident correlation " +
            "{IncidentCorrelationId}; occurred {OccurredAtUtc}")]
    private static partial void LogNotice(
        ILogger logger,
        string securitySignalCode,
        string securitySignalCategory,
        string securitySignalSeverity,
        string incidentCorrelationId,
        DateTimeOffset occurredAtUtc);

    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message =
            "Security signal {SecuritySignalCode} in category {SecuritySignalCategory} " +
            "at severity {SecuritySignalSeverity}; incident correlation " +
            "{IncidentCorrelationId}; occurred {OccurredAtUtc}")]
    private static partial void LogWarning(
        ILogger logger,
        string securitySignalCode,
        string securitySignalCategory,
        string securitySignalSeverity,
        string incidentCorrelationId,
        DateTimeOffset occurredAtUtc);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Critical,
        Message =
            "Security signal {SecuritySignalCode} in category {SecuritySignalCategory} " +
            "at severity {SecuritySignalSeverity}; incident correlation " +
            "{IncidentCorrelationId}; occurred {OccurredAtUtc}")]
    private static partial void LogCritical(
        ILogger logger,
        string securitySignalCode,
        string securitySignalCategory,
        string securitySignalSeverity,
        string incidentCorrelationId,
        DateTimeOffset occurredAtUtc);
}
