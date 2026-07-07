namespace Gma.Framework.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Gma.Framework.Api.Results;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ResultExtensionsTests
{
    [Fact]
    public void Non_generic_success_maps_to_no_content()
    {
        IResult result = Result.Success().ToHttpResult();

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public void Generic_success_maps_to_ok_value()
    {
        IResult result = Result.Success("value").ToHttpResult();

        Ok<string> ok = Assert.IsType<Ok<string>>(result);
        Assert.Equal("value", ok.Value);
    }

    [Fact]
    public void Failure_maps_to_problem_with_configured_status_code()
    {
        Error error = new("Test.NotFound", "Missing.");
        ApiErrorStatusCodeMap map = ApiErrorStatusCodeMap.Create(
            new ApiErrorStatusCode(error.Code, StatusCodes.Status404NotFound));

        IResult result = Result.Failure<string>(error).ToHttpResult(map);

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(error.Code, problem.ProblemDetails.Title);
        Assert.Equal(error.Message, problem.ProblemDetails.Detail);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public void Failure_without_configured_status_code_defaults_to_bad_request()
    {
        Error error = new("Test.Error", "Something failed.");

        IResult result = Result.Failure(error).ToHttpResult();

        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public void Null_results_are_rejected_explicitly()
    {
        Result result = null!;
        Result<string> genericResult = null!;

        Assert.Throws<ArgumentNullException>(() => result.ToHttpResult());
        Assert.Throws<ArgumentNullException>(() => genericResult.ToHttpResult());
    }
}
