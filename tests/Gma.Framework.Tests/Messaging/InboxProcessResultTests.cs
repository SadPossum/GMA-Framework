namespace Gma.Framework.Tests;

using System.Reflection;
using Gma.Framework.Messaging;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InboxProcessResultTests
{
    [Theory]
    [InlineData(InboxProcessStatus.Processed)]
    [InlineData(InboxProcessStatus.Duplicate)]
    [InlineData(InboxProcessStatus.Suppressed)]
    public void Non_failed_results_have_no_error(InboxProcessStatus expectedStatus)
    {
        InboxProcessResult result = expectedStatus switch
        {
            InboxProcessStatus.Processed => InboxProcessResult.Processed(),
            InboxProcessStatus.Duplicate => InboxProcessResult.Duplicate(),
            InboxProcessStatus.Suppressed => InboxProcessResult.Suppressed(),
            _ => throw new InvalidOperationException()
        };

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failed_normalizes_error()
    {
        InboxProcessResult result = InboxProcessResult.Failed(" handler failed ");

        Assert.Equal(InboxProcessStatus.Failed, result.Status);
        Assert.Equal("handler failed", result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Failed_rejects_blank_error(string error)
    {
        Assert.Throws<ArgumentException>(() => InboxProcessResult.Failed(error));
    }

    [Fact]
    public void Failed_replaces_control_characters_in_error()
    {
        InboxProcessResult result = InboxProcessResult.Failed($"handler{char.MinValue}failed");

        Assert.Equal("handler failed", result.Error);
    }

    [Fact]
    public void Failed_truncates_overlong_error()
    {
        InboxProcessResult result = InboxProcessResult.Failed(new string('x', InboxProcessResult.ErrorMaxLength + 1));

        Assert.Equal(InboxProcessResult.ErrorMaxLength, result.Error?.Length);
    }

    [Fact]
    public void Result_must_be_created_through_factories()
    {
        ConstructorInfo[] publicConstructors = typeof(InboxProcessResult)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.Empty(publicConstructors);
    }
}
