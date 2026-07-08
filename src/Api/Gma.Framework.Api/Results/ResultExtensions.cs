namespace Gma.Framework.Api.Results;

using Microsoft.AspNetCore.Http;
using Gma.Framework.Results;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, ApiErrorStatusCodeMap? statusCodeMap = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.NoContent()
            : ToProblem(result.Error, statusCodeMap);
    }

    public static IResult ToHttpResult<TValue>(this Result<TValue> result, ApiErrorStatusCodeMap? statusCodeMap = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(result.Error, statusCodeMap);
    }

    private static IResult ToProblem(Error error, ApiErrorStatusCodeMap? statusCodeMap) =>
        Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: (statusCodeMap ?? ApiErrorStatusCodeMap.Empty).GetStatusCode(error));
}
