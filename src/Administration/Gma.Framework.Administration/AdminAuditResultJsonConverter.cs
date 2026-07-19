namespace Gma.Framework.Administration;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class AdminAuditResultJsonConverter : JsonConverter<AdminAuditResult>
{
    public override AdminAuditResult Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String ||
            !AdminAuditResults.TryParse(reader.GetString(), out AdminAuditResult result))
        {
            throw new JsonException("Admin audit result must be a known string value.");
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        AdminAuditResult value,
        JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(AdminAuditResults.ToWireName(value));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException("Admin audit result is invalid.", exception);
        }
    }
}
