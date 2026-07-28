namespace Gma.Framework.Observability;

public sealed record SecuritySignalRecord
{
    public const int IncidentCorrelationIdLength = 32;

    public SecuritySignalRecord(
        SecuritySignalDefinition definition,
        string incidentCorrelationId,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);

        this.SignalCode = definition.Code;
        this.Category = definition.Category;
        this.Severity = definition.Severity;
        this.IncidentCorrelationId = NormalizeIncidentCorrelationId(incidentCorrelationId);
        this.OccurredAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public string SignalCode { get; }
    public SecuritySignalCategory Category { get; }
    public SecuritySignalSeverity Severity { get; }
    public string IncidentCorrelationId { get; }
    public DateTimeOffset OccurredAtUtc { get; }

    private static string NormalizeIncidentCorrelationId(string incidentCorrelationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentCorrelationId);

        string normalized = incidentCorrelationId.Trim();
        if (normalized.Length != IncidentCorrelationIdLength ||
            normalized.Any(char.IsAsciiLetterUpper) ||
            normalized.Any(character =>
                !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                $"Incident correlation id must contain exactly {IncidentCorrelationIdLength} lowercase hexadecimal characters.",
                nameof(incidentCorrelationId));
        }

        return normalized;
    }
}
