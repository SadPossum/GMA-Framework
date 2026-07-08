namespace Gma.Framework.Notifications;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class NotificationSeverityJsonConverter : JsonConverter<NotificationSeverity>
{
    public override NotificationSeverity Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Notification severity must be a string.");
        }

        string? value = reader.GetString();
        if (TryParse(value, out NotificationSeverity severity))
        {
            return severity;
        }

        throw new JsonException("Notification severity is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        NotificationSeverity value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(ToWireName(value));

    private static bool TryParse(string? value, out NotificationSeverity severity)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (Enum.TryParse(normalized, ignoreCase: true, out severity) &&
            severity is not NotificationSeverity.Unknown &&
            Enum.IsDefined(severity))
        {
            return true;
        }

        severity = normalized.ToLowerInvariant() switch
        {
            "info" => NotificationSeverity.Info,
            "success" => NotificationSeverity.Success,
            "warning" => NotificationSeverity.Warning,
            "error" => NotificationSeverity.Error,
            _ => NotificationSeverity.Unknown
        };

        return severity is not NotificationSeverity.Unknown;
    }

    private static string ToWireName(NotificationSeverity severity) =>
        severity switch
        {
            NotificationSeverity.Info => "info",
            NotificationSeverity.Success => "success",
            NotificationSeverity.Warning => "warning",
            NotificationSeverity.Error => "error",
            _ => throw new JsonException("Notification severity is invalid.")
        };
}
