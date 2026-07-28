namespace Gma.Framework.Tests;

using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminResourceScopeTests
{
    [Fact]
    public void Segment_normalizes_name_and_value()
    {
        AdminResourceScopeSegment segment = AdminResourceScopeSegment.Create(
            " Property ",
            " 9e11c484-2999-44d5-b293-2c0962fdff76 ");

        Assert.Equal("property", segment.Name);
        Assert.Equal("9e11c484-2999-44d5-b293-2c0962fdff76", segment.Value);
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("Not Valid", "value")]
    [InlineData("property", "has whitespace")]
    [InlineData("property", "has/slash")]
    [InlineData("property", "has:colon")]
    public void Segment_rejects_invalid_values(string name, string value)
    {
        Assert.False(AdminResourceScopeSegment.TryCreate(name, value, out _));
        Assert.Throws<ArgumentException>(() => AdminResourceScopeSegment.Create(name, value));
    }

    [Fact]
    public void Scope_requires_at_least_one_segment_and_copies_input()
    {
        AdminResourceScopeSegment[] segments =
        [
            AdminResourceScopeSegment.Create("property", "property-a")
        ];

        AdminResourceScope scope = AdminResourceScope.Create(segments);
        segments[0] = AdminResourceScopeSegment.Create("property", "property-b");

        Assert.Equal("property-a", Assert.Single(scope.Segments).Value);
        Assert.Equal("property:property-a", scope.Value);
        Assert.Equal(scope.Value, scope.ToString());
        Assert.Throws<ArgumentException>(() => AdminResourceScope.Create([]));
    }

    [Theory]
    [InlineData("property:property-a", "property:property-a")]
    [InlineData("site:site-a/unit:unit-1", "site:site-a/unit:unit-1")]
    public void Parse_accepts_canonical_segments(string input, string expected)
    {
        AdminResourceScope scope = AdminResourceScope.Parse(input);

        Assert.Equal(expected, scope.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("global")]
    [InlineData("property")]
    [InlineData("property:one:two")]
    [InlineData("property:one/")]
    public void Parse_rejects_invalid_values(string input)
    {
        Assert.False(AdminResourceScope.TryParse(input, out _));
        Assert.Throws<ArgumentException>(() => AdminResourceScope.Parse(input));
    }
}
