namespace Gma.Framework.Tests;

using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ErrorTests
{
    [Fact]
    public void Constructor_normalizes_valid_error()
    {
        Error error = new(" Test.Error ", " Something failed. ");

        Assert.Equal("Test.Error", error.Code);
        Assert.Equal("Something failed.", error.Message);
        Assert.Equal(new Error("Test.Error", "Something failed."), error);
    }

    [Fact]
    public void None_is_the_only_empty_error_sentinel()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Message);

        Assert.Throws<ArgumentException>(() => new Error(string.Empty, "Something failed."));
        Assert.Throws<ArgumentException>(() => new Error("Test.Error", string.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Test")]
    [InlineData(".Test")]
    [InlineData("Test.")]
    [InlineData("Test..Error")]
    [InlineData("Test Error")]
    [InlineData("Test-Error")]
    [InlineData("Test_Error")]
    public void Constructor_rejects_invalid_error_codes(string code)
    {
        Assert.Throws<ArgumentException>(() => new Error(code, "Something failed."));
    }

    [Fact]
    public void Constructor_rejects_control_characters_in_error_codes()
    {
        Assert.Throws<ArgumentException>(() => new Error($"Test{char.MinValue}.Error", "Something failed."));
    }

    [Fact]
    public void Constructor_rejects_overlong_error_codes()
    {
        Assert.Throws<ArgumentException>(() => new Error($"Test.{new string('x', Error.CodeMaxLength)}", "Something failed."));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_rejects_blank_messages(string message)
    {
        Assert.Throws<ArgumentException>(() => new Error("Test.Error", message));
    }

    [Fact]
    public void Constructor_rejects_control_characters_in_messages()
    {
        Assert.Throws<ArgumentException>(() => new Error("Test.Error", $"Something{char.MinValue}failed."));
    }

    [Fact]
    public void Constructor_rejects_overlong_messages()
    {
        Assert.Throws<ArgumentException>(() => new Error("Test.Error", new string('x', Error.MessageMaxLength + 1)));
    }

    [Fact]
    public void Normalize_code_returns_trimmed_valid_code()
    {
        Assert.Equal("Test.Error404", Error.NormalizeCode(" Test.Error404 "));
    }

    [Fact]
    public void Try_normalize_code_rejects_invalid_code()
    {
        Assert.False(Error.TryNormalizeCode("Test Error", out string? normalized));
        Assert.Null(normalized);
    }
}
