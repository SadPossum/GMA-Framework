namespace Gma.Framework.Api.Results;

using Microsoft.AspNetCore.Http;
using Gma.Framework.Results;

public readonly record struct ApiErrorStatusCode
{
    public const int MinimumStatusCode = StatusCodes.Status400BadRequest;
    public const int MaximumStatusCode = 599;

    public ApiErrorStatusCode(string errorCode, int statusCode)
    {
        if (statusCode is < MinimumStatusCode or > MaximumStatusCode)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statusCode),
                $"API error status codes must be in the {MinimumStatusCode}..{MaximumStatusCode} range.");
        }

        this.ErrorCode = Error.NormalizeCode(errorCode);
        this.StatusCode = statusCode;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }
}
