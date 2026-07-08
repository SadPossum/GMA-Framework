namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IntegrationEventNamingTests
{
    [Fact]
    public void Create_subject_normalizes_public_contract_parts()
    {
        string subject = IntegrationEventNaming.CreateSubject(
            " GMA ",
            " Catalog ",
            " Item-Created ",
            2);

        Assert.Equal("gma.catalog.item-created.v2", subject);
    }

    [Theory]
    [InlineData("gma.catalog.item-created.v1", "gma.catalog.item-created.v1")]
    [InlineData(" GMA.Catalog.Item-Created.V1 ", "gma.catalog.item-created.v1")]
    public void Normalize_subject_accepts_contract_shape(string value, string expected)
    {
        Assert.Equal(expected, IntegrationEventNaming.NormalizeSubject(value));
    }

    [Theory]
    [InlineData("gma.catalog.item-created")]
    [InlineData("gma.catalog.item-created.v0")]
    [InlineData("gma.catalog.item-created.v01")]
    [InlineData("gma.catalog.item-created.v1.extra")]
    [InlineData("gma.catalog.item created.v1")]
    [InlineData("gma.catalog-.item-created.v1")]
    public void Normalize_subject_rejects_invalid_contract_shape(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => IntegrationEventNaming.NormalizeSubject(value));
    }

    [Theory]
    [InlineData("catalog-item-created-projection", true)]
    [InlineData("Catalog-Item-Created-Projection", true)]
    [InlineData("catalog.item-created-projection", false)]
    [InlineData("catalog item created projection", false)]
    [InlineData("catalog--item-created-projection", false)]
    [InlineData("-catalog-item-created-projection", false)]
    public void Handler_names_use_kebab_segments(string value, bool isValid)
    {
        if (isValid)
        {
            Assert.Equal(value.Trim().ToLowerInvariant(), IntegrationEventNaming.NormalizeHandlerName(value));
            return;
        }

        Assert.Throws<ArgumentException>(() => IntegrationEventNaming.NormalizeHandlerName(value));
    }
}
