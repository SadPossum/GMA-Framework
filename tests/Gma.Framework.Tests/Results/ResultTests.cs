namespace Gma.Framework.Tests;

using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_carries_error()
    {
        Error error = new("Test.Error", "Something failed.");

        Result result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Failure_rejects_null_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result.Failure<string>(null!));
    }

    [Fact]
    public void Failure_rejects_none_error()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
        Assert.Throws<InvalidOperationException>(() => Result.Failure<string>(Error.None));
    }

    [Fact]
    public void Generic_success_returns_value()
    {
        Result<string> result = Result.Success("value");

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Generic_success_rejects_null_value()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
        Assert.Throws<ArgumentNullException>(() => Result.Success<int?>(null));
    }

    [Fact]
    public void Generic_failure_value_cannot_be_accessed()
    {
        Result<string> result = Result.Failure<string>(new Error("Test.Error", "Something failed."));

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
