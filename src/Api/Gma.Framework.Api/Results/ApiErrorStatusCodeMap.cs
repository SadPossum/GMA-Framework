namespace Gma.Framework.Api.Results;

using Microsoft.AspNetCore.Http;
using Gma.Framework.Results;

public sealed class ApiErrorStatusCodeMap
{
    public static ApiErrorStatusCodeMap Empty { get; } = new([]);

    private readonly Dictionary<string, int> statusCodesByErrorCode;

    private ApiErrorStatusCodeMap(IEnumerable<ApiErrorStatusCode> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Dictionary<string, int> map = new(StringComparer.Ordinal);

        foreach (ApiErrorStatusCode entry in entries)
        {
            string errorCode = Error.NormalizeCode(entry.ErrorCode);

            if (entry.StatusCode is < ApiErrorStatusCode.MinimumStatusCode or > ApiErrorStatusCode.MaximumStatusCode)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), "API error status codes must be in the 4xx or 5xx range.");
            }

            if (!map.TryAdd(errorCode, entry.StatusCode))
            {
                throw new ArgumentException($"Duplicate error code mapping '{errorCode}'.", nameof(entries));
            }
        }

        this.statusCodesByErrorCode = map;
    }

    public static ApiErrorStatusCodeMap Create(params ApiErrorStatusCode[] entries) => new(entries);

    public int GetStatusCode(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return this.statusCodesByErrorCode.TryGetValue(error.Code, out int statusCode)
            ? statusCode
            : StatusCodes.Status400BadRequest;
    }
}
