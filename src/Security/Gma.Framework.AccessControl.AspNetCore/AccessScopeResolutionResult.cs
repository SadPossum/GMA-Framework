namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Microsoft.AspNetCore.Http;

public sealed record AccessScopeResolutionResult(
    bool IsSuccess,
    AccessScope? Scope,
    string? ErrorCode,
    string? ErrorMessage,
    int StatusCode)
{
    public static AccessScopeResolutionResult Success(AccessScope scope) =>
        new(true, scope, null, null, StatusCodes.Status200OK);

    public static AccessScopeResolutionResult Failure(string errorCode, string errorMessage, int statusCode) =>
        new(false, null, errorCode, errorMessage, statusCode);
}
