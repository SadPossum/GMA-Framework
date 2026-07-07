namespace Gma.Framework.Tests;

using Gma.Framework.Api.Observability;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ModuleEndpointMetadataTests
{
    [Fact]
    public void Module_name_is_normalized()
    {
        ModuleEndpointMetadata metadata = new(" Auth ");

        Assert.Equal("auth", metadata.ModuleName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-auth")]
    [InlineData("auth-")]
    [InlineData("auth_api")]
    [InlineData("auth..api")]
    [InlineData("auth api")]
    public void Module_name_must_be_kebab_case_segment(string moduleName)
    {
        Assert.Throws<ArgumentException>(() => new ModuleEndpointMetadata(moduleName));
    }
}
