namespace Gma.Framework.Tests;

using Gma.Framework.Observability.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ModuleNameResolverTests
{
    [Theory]
    [InlineData("Gma.Modules.Auth.Application", "auth")]
    [InlineData("CustomerSupport.Application", "customer-support")]
    [InlineData("HTTPApi.Application", "http-api")]
    [InlineData("Gma.Framework.Tests", "framework")]
    [InlineData("Gma.Modules.CustomerSupport.Application", "customer-support")]
    [InlineData("BunkFy.Modules.Reservations.Application", "reservations")]
    [InlineData("Contoso.Platform.Modules.Inventory.Application", "inventory")]
    public void Resolves_assembly_prefix_to_kebab_case_module_name(
        string assemblyName,
        string expectedModuleName)
    {
        Assert.Equal(expectedModuleName, ModuleNameResolver.FromAssemblyName(assemblyName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Customer_Support.Application")]
    public void Rejects_invalid_assembly_module_prefixes(string assemblyName)
    {
        Assert.ThrowsAny<ArgumentException>(() => ModuleNameResolver.FromAssemblyName(assemblyName));
    }
}
