namespace Gma.Framework.Tests;

using Gma.Framework.Naming;
using Xunit;

[Trait("Category", "Unit")]
public sealed class SharedModuleNamesTests
{
    [Theory]
    [InlineData("auth", "auth")]
    [InlineData(" Auth ", "auth")]
    [InlineData("task-runtime", "task-runtime")]
    public void Normalize_accepts_lowercase_kebab_module_names(string value, string expected)
    {
        Assert.Equal(expected, SharedModuleNames.Normalize(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-auth")]
    [InlineData("auth-")]
    [InlineData("auth_api")]
    [InlineData("auth.api")]
    [InlineData("auth api")]
    public void Normalize_rejects_values_that_are_not_single_kebab_segments(string value)
    {
        Assert.Throws<ArgumentException>(() => SharedModuleNames.Normalize(value));
    }
}
