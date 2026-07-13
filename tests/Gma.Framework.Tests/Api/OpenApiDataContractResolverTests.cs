namespace Gma.Framework.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using Gma.Framework.Api.OpenApi;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OpenApiDataContractResolverTests
{
    [Fact]
    public void Strict_enum_contract_omits_non_serializable_unknown_sentinel()
    {
        var resolver = new GmaOpenApiDataContractResolver(
            Options.Create(new JsonOptions()));

        DataContract contract = resolver.GetDataContractForType(typeof(StrictStatus));
        string[] values = typeof(StrictStatus)
            .GetEnumValues()
            .Cast<object>()
            .Select(contract.JsonConverter)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(DataType.String, contract.DataType);
        Assert.Equal(["\"ready\""], values);
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(StrictStatus.Unknown));
    }

    [JsonConverter(typeof(StrictStatusJsonConverter))]
    private enum StrictStatus
    {
        Unknown = 0,
        Ready = 1,
    }

    private sealed class StrictStatusJsonConverter : JsonConverter<StrictStatus>
    {
        public override StrictStatus Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String && reader.GetString() == "ready"
                ? StrictStatus.Ready
                : throw new JsonException("Strict status is invalid.");

        public override void Write(
            Utf8JsonWriter writer,
            StrictStatus value,
            JsonSerializerOptions options)
        {
            if (value != StrictStatus.Ready)
            {
                throw new JsonException("Strict status is invalid.");
            }

            writer.WriteStringValue("ready");
        }
    }
}
