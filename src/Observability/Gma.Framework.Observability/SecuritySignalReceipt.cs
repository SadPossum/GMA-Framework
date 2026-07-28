namespace Gma.Framework.Observability;

public sealed record SecuritySignalReceipt(string IncidentCorrelationId, bool WasEmitted);
