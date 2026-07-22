namespace Gma.Framework.Tests.Runtime;

using Gma.Framework.Runtime.Failures;
using Xunit;

public sealed class RuntimeFailureDescriptionsTests
{
    [Fact]
    public void From_exception_keeps_only_stable_code_and_exception_type()
    {
        const string personalDataCanary = "guest.aprokudanov@example.test";

        string description = RuntimeFailureDescriptions.FromException(
            "handler-failed",
            new InvalidOperationException(personalDataCanary));

        Assert.Equal("handler-failed:InvalidOperationException", description);
        Assert.DoesNotContain(personalDataCanary, description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Handler Failed")]
    [InlineData("handler.failed")]
    public void From_exception_rejects_unstable_failure_codes(string failureCode)
    {
        Assert.Throws<ArgumentException>(() =>
            RuntimeFailureDescriptions.FromException(failureCode, new InvalidOperationException()));
    }
}
