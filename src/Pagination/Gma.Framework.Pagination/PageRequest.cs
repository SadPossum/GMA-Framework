namespace Gma.Framework.Pagination;

public readonly record struct PageRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxPage = int.MaxValue / MaxPageSize;

    private readonly int page;
    private readonly int pageSize;

    public PageRequest(int page, int pageSize)
    {
        this.page = NormalizePage(page);
        this.pageSize = NormalizePageSize(pageSize);
    }

    public int Page => this.page == 0 ? DefaultPage : this.page;
    public int PageSize => this.pageSize == 0 ? DefaultPageSize : this.pageSize;
    public int SkipCount => checked((this.Page - 1) * this.PageSize);

    public static PageRequest Normalize(int page, int pageSize) =>
        new(page, pageSize);

    public bool Equals(PageRequest other) =>
        this.Page == other.Page && this.PageSize == other.PageSize;

    public override int GetHashCode() =>
        HashCode.Combine(this.Page, this.PageSize);

    private static int NormalizePage(int page) =>
        Math.Clamp(page, DefaultPage, MaxPage);

    private static int NormalizePageSize(int pageSize) =>
        Math.Clamp(pageSize, 1, MaxPageSize);
}
