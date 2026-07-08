namespace Gma.Framework.Messaging;

public sealed record InboxProcessResult
{
    public const int ErrorMaxLength = 2048;

    private InboxProcessResult(InboxProcessStatus status, string? error)
    {
        this.Status = status;
        this.Error = error;
    }

    public InboxProcessStatus Status { get; }
    public string? Error { get; }

    public static InboxProcessResult Processed() => new(InboxProcessStatus.Processed, null);
    public static InboxProcessResult Duplicate() => new(InboxProcessStatus.Duplicate, null);
    public static InboxProcessResult Failed(string error) => new(InboxProcessStatus.Failed, NormalizeError(error));

    public static string NormalizeError(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        string normalized = new(error
            .Trim()
            .Select(character => char.IsControl(character) ? ' ' : character)
            .ToArray());

        return normalized.Length > ErrorMaxLength
            ? normalized[..ErrorMaxLength]
            : normalized;
    }
}
