namespace Gma.Framework.Tasks;

public sealed record TaskRunDetails(
    TaskRunSummary Summary,
    string PayloadJson,
    string? NodeId,
    DateTimeOffset? LeasedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? CancellationRequestedAtUtc,
    string? CancellationRequestedBy);
