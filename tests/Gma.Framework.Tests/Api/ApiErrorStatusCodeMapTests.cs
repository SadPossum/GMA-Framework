namespace Gma.Framework.Tests;

using Microsoft.AspNetCore.Http;
using Gma.Framework.Api.Results;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ApiErrorStatusCodeMapTests
{
    [Fact]
    public void Empty_map_defaults_errors_to_bad_request()
    {
        int statusCode = ApiErrorStatusCodeMap.Empty.GetStatusCode(new Error("Test.Error", "Something failed."));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
    }

    [Fact]
    public void Map_returns_configured_status_code()
    {
        ApiErrorStatusCodeMap map = ApiErrorStatusCodeMap.Create(
            new("Test.NotFound", StatusCodes.Status404NotFound),
            new("Test.Conflict", StatusCodes.Status409Conflict));

        Assert.Equal(StatusCodes.Status404NotFound, map.GetStatusCode(new Error("Test.NotFound", "Missing.")));
        Assert.Equal(StatusCodes.Status409Conflict, map.GetStatusCode(new Error("Test.Conflict", "Conflict.")));
        Assert.Equal(StatusCodes.Status400BadRequest, map.GetStatusCode(new Error("Test.Other", "Other.")));
    }

    [Fact]
    public void Status_code_entry_normalizes_error_codes()
    {
        ApiErrorStatusCode entry = new(" Test.NotFound ", StatusCodes.Status404NotFound);
        ApiErrorStatusCodeMap map = ApiErrorStatusCodeMap.Create(
            entry);

        Assert.Equal("Test.NotFound", entry.ErrorCode);
        Assert.Equal(StatusCodes.Status404NotFound, map.GetStatusCode(new Error("Test.NotFound", "Missing.")));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Test")]
    [InlineData("Test Error")]
    public void Status_code_entry_rejects_invalid_error_codes(string errorCode)
    {
        Assert.Throws<ArgumentException>(() =>
            new ApiErrorStatusCode(errorCode, StatusCodes.Status404NotFound));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(399)]
    [InlineData(600)]
    public void Status_code_entry_rejects_non_error_status_codes(int statusCode)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ApiErrorStatusCode("Test.Error", statusCode));
    }

    [Fact]
    public void Map_rejects_default_entry()
    {
        ApiErrorStatusCode entry = default;

        Assert.Throws<ArgumentException>(() => ApiErrorStatusCodeMap.Create(entry));
    }

    [Fact]
    public void Map_rejects_null_entry_collection()
    {
        Assert.Throws<ArgumentNullException>(() => ApiErrorStatusCodeMap.Create(null!));
    }

    [Fact]
    public void Map_rejects_duplicate_error_codes()
    {
        Assert.Throws<ArgumentException>(() =>
            ApiErrorStatusCodeMap.Create(
                new("Test.Error", StatusCodes.Status404NotFound),
                new(" Test.Error ", StatusCodes.Status409Conflict)));
    }
}
