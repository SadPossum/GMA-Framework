namespace Gma.Framework.Tests;

using Gma.Framework.Naming;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantIdsTests
{
    [Theory]
    [InlineData("tenant-a", "tenant-a")]
    [InlineData(" Tenant-A ", "Tenant-A")]
    [InlineData("tenant:eu-west:42", "tenant:eu-west:42")]
    public void Normalize_trims_valid_tenant_ids_without_changing_case(string value, string expected)
    {
        Assert.Equal(expected, TenantIds.Normalize(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("tenant a")]
    [InlineData("tenant\tid")]
    public void Normalize_rejects_blank_whitespace_or_control_characters(string value)
    {
        Assert.Throws<ArgumentException>(() => TenantIds.Normalize(value));
    }

    [Fact]
    public void Normalize_rejects_values_longer_than_the_shared_limit()
    {
        Assert.Throws<ArgumentException>(() => TenantIds.Normalize(new string('x', TenantIds.MaxLength + 1)));
    }
}
