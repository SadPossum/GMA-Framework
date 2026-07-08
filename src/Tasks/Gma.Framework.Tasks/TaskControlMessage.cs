namespace Gma.Framework.Tasks;

public sealed record TaskControlMessage
{
    public const int PayloadMaxLength = 64 * 1024;

    public TaskControlMessage(
        Guid messageId,
        Guid runId,
        string commandName,
        string payloadJson,
        DateTimeOffset enqueuedAtUtc,
        string? requestedBy = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        this.MessageId = RequireId(messageId, nameof(messageId));
        this.RunId = RequireId(runId, nameof(runId));
        this.CommandName = TaskNames.NormalizeControlCommandName(commandName, nameof(commandName));
        this.PayloadJson = NormalizePayload(payloadJson);
        this.EnqueuedAtUtc = TaskRunRequest.RequireTimestamp(enqueuedAtUtc, nameof(enqueuedAtUtc));
        this.RequestedBy = TaskNames.NormalizeOptionalActor(requestedBy, nameof(requestedBy));
        this.ExpiresAtUtc = expiresAtUtc;

        if (expiresAtUtc is not null && expiresAtUtc <= enqueuedAtUtc)
        {
            throw new ArgumentException("Control message expiration must be after the enqueue timestamp.", nameof(expiresAtUtc));
        }
    }

    public Guid MessageId { get; }
    public Guid RunId { get; }
    public string CommandName { get; }
    public string PayloadJson { get; }
    public DateTimeOffset EnqueuedAtUtc { get; }
    public string? RequestedBy { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }

    private static Guid RequireId(Guid id, string parameterName) =>
        id == Guid.Empty
            ? throw new ArgumentException($"{parameterName} must not be empty.", parameterName)
            : id;

    private static string NormalizePayload(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);

        if (payloadJson.Length > PayloadMaxLength)
        {
            throw new ArgumentException(
                $"Control message payload must be {PayloadMaxLength} characters or fewer.",
                nameof(payloadJson));
        }

        return payloadJson;
    }
}
