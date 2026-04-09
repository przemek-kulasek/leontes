namespace Leontes.Application;

public sealed record PagedRequest(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null)
{
    public int Page { get; } = Page < 1 ? 1 : Page;
    public int PageSize { get; } = PageSize < 1 ? 1 : PageSize > 100 ? 100 : PageSize;
    public int Skip => (Page - 1) * PageSize;
}
