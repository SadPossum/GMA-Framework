namespace Gma.Framework.Tasks;

public sealed record TaskProgress
{
    public const int MessageMaxLength = 1024;

    public TaskProgress(int percentComplete, string? message = null)
    {
        if (percentComplete is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentComplete),
                percentComplete,
                "Task progress percent must be between 0 and 100.");
        }

        this.PercentComplete = percentComplete;
        this.Message = NormalizeMessage(message);
    }

    public int PercentComplete { get; }
    public string? Message { get; }

    private static string? NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string normalized = message.Trim();
        if (normalized.Length > MessageMaxLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Task progress message must be {MessageMaxLength} characters or fewer and cannot contain control characters.",
                nameof(message));
        }

        return normalized;
    }
}
