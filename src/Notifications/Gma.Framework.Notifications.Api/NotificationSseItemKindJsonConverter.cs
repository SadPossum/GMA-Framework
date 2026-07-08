namespace Gma.Framework.Notifications.Api;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class NotificationSseItemKindJsonConverter : JsonConverter<NotificationSseItemKind>
{
    public override NotificationSseItemKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException("Notification SSE item kind must be a string.");
        }

        return NotificationSseItemKindNames.TryParse(reader.GetString(), out NotificationSseItemKind kind)
            ? kind
            : throw new JsonException("Notification SSE item kind is invalid.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        NotificationSseItemKind value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(NotificationSseItemKindNames.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Notification SSE item kind is invalid.", exception);
        }
    }
}
