namespace Gma.Framework.Tests;

using Gma.Framework.Pagination;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PageRequestTests
{
    [Theory]
    [InlineData(-10, 0, PageRequest.DefaultPage, 1)]
    [InlineData(0, -5, PageRequest.DefaultPage, 1)]
    [InlineData(2, 250, 2, PageRequest.MaxPageSize)]
    [InlineData(int.MaxValue, int.MaxValue, PageRequest.MaxPage, PageRequest.MaxPageSize)]
    public void Normalize_bounds_page_and_page_size(
        int page,
        int pageSize,
        int expectedPage,
        int expectedPageSize)
    {
        PageRequest request = PageRequest.Normalize(page, pageSize);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedPageSize, request.PageSize);
    }

    [Theory]
    [InlineData(-10, 0, PageRequest.DefaultPage, 1)]
    [InlineData(0, -5, PageRequest.DefaultPage, 1)]
    [InlineData(2, 250, 2, PageRequest.MaxPageSize)]
    [InlineData(int.MaxValue, int.MaxValue, PageRequest.MaxPage, PageRequest.MaxPageSize)]
    public void Constructor_bounds_page_and_page_size(
        int page,
        int pageSize,
        int expectedPage,
        int expectedPageSize)
    {
        PageRequest request = new(page, pageSize);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedPageSize, request.PageSize);
    }

    [Fact]
    public void Normalized_values_do_not_overflow_common_skip_calculation()
    {
        PageRequest request = PageRequest.Normalize(int.MaxValue, int.MaxValue);

        Assert.True(request.SkipCount >= 0);
    }

    [Fact]
    public void Skip_count_uses_normalized_page_and_page_size()
    {
        PageRequest request = PageRequest.Normalize(3, 10);

        Assert.Equal(20, request.SkipCount);
    }

    [Fact]
    public void Default_struct_instance_uses_logical_default_page_request()
    {
        PageRequest request = default;

        Assert.Equal(PageRequest.DefaultPage, request.Page);
        Assert.Equal(PageRequest.DefaultPageSize, request.PageSize);
        Assert.Equal(0, request.SkipCount);
        Assert.Equal(PageRequest.Normalize(PageRequest.DefaultPage, PageRequest.DefaultPageSize), request);
    }
}

