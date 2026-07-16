namespace Gma.Framework.Api.Tenancy;

using Microsoft.AspNetCore.Http;

public sealed record TenantEndpointAccessDecision
{
    private TenantEndpointAccessDecision(
        bool isAllowed,
        string? errorCode,
        string? errorMessage,
        int statusCode)
    {
        if (isAllowed)
        {
            if (errorCode is not null || errorMessage is not null || statusCode != StatusCodes.Status200OK)
            {
                throw new ArgumentException("An allowed tenant access decision cannot carry an error.");
            }
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
            if (statusCode is < StatusCodes.Status400BadRequest or > 599)
            {
                throw new ArgumentOutOfRangeException(nameof(statusCode), "A denied tenant access decision requires an HTTP error status code.");
            }
        }

        this.IsAllowed = isAllowed;
        this.ErrorCode = errorCode;
        this.ErrorMessage = errorMessage;
        this.StatusCode = statusCode;
    }

    public static TenantEndpointAccessDecision Allowed { get; } =
        new(true, null, null, StatusCodes.Status200OK);

    public bool IsAllowed { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public int StatusCode { get; }

    public static TenantEndpointAccessDecision Denied(
        string errorCode,
        string errorMessage,
        int statusCode = StatusCodes.Status403Forbidden) =>
        new(false, errorCode, errorMessage, statusCode);
}
