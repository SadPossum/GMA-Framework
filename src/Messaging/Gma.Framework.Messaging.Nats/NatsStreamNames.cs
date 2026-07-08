namespace Gma.Framework.Messaging.Nats;

internal static class NatsStreamNames
{
    public const int MaxLength = 128;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9"
    };

    public static string Normalize(string streamName, string parameterName = "streamName")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName, parameterName);

        if (!IsValid(streamName))
        {
            throw new ArgumentException(
                $"{parameterName} must be 1-{MaxLength} characters, use only ASCII letters, digits, '-' or '_', and avoid reserved device names.",
                parameterName);
        }

        return streamName;
    }

    public static bool IsValid(string? streamName)
    {
        if (string.IsNullOrWhiteSpace(streamName) ||
            streamName.Length > MaxLength ||
            ReservedDeviceNames.Contains(streamName))
        {
            return false;
        }

        return streamName.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_');
    }
}
