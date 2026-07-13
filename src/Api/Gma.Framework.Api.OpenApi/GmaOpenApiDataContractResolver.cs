namespace Gma.Framework.Api.OpenApi;

using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

internal sealed class GmaOpenApiDataContractResolver(IOptions<JsonOptions> jsonOptions)
    : ISerializerDataContractResolver
{
    private readonly JsonSerializerOptions serializerOptions =
        new(jsonOptions.Value.SerializerOptions);

    private readonly JsonSerializerDataContractResolver inner =
        new(new JsonSerializerOptions(jsonOptions.Value.SerializerOptions));

    public DataContract GetDataContractForType(Type type)
    {
        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (!effectiveType.IsEnum)
        {
            return this.inner.GetDataContractForType(type);
        }

        object[] values = effectiveType.GetEnumValues().Cast<object>().ToArray();
        string[] serializedValues = values
            .Select(value => this.TrySerialize(value, effectiveType))
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        if (serializedValues.Length == values.Length || serializedValues.Length == 0)
        {
            return this.inner.GetDataContractForType(type);
        }

        string fallback = serializedValues[0];
        (DataType dataType, string? dataFormat) = ResolvePrimitiveType(effectiveType, fallback);
        return DataContract.ForPrimitive(
            effectiveType,
            dataType,
            dataFormat,
            value => this.TrySerialize(value, effectiveType) ?? fallback);
    }

    private string? TrySerialize(object value, Type type)
    {
        try
        {
            return JsonSerializer.Serialize(value, type, this.serializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static (DataType DataType, string? DataFormat) ResolvePrimitiveType(
        Type enumType,
        string serializedValue)
    {
        if (serializedValue.StartsWith('"'))
        {
            return (DataType.String, null);
        }

        Type underlyingType = enumType.GetEnumUnderlyingType();
        string format = underlyingType == typeof(long) || underlyingType == typeof(ulong)
            ? "int64"
            : "int32";
        return (DataType.Integer, format);
    }
}
