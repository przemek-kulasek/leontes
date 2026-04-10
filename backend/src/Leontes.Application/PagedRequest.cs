namespace Leontes.Application;

public sealed record PagedRequest
{
    public PagedRequest(
        int page = 1,
        int pageSize = 20,
        string? sortBy = null,
        string? sortDirection = null)
    {
        Page = page < 1 ? 1 : page;
        PageSize = pageSize < 1 ? 1 : pageSize > 100 ? 100 : pageSize;
        SortBy = sortBy;
        SortDirection = sortDirection;
    }

    public int Page { get; }
    public int PageSize { get; }
    public string? SortBy { get; }
    public string? SortDirection { get; }
    public int Skip => (Page - 1) * PageSize;
}
